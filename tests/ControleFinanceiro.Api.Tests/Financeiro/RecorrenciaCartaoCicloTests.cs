using System.Net.Http.Json;
using System.Text.Json;
using ControleFinanceiro.Api.Configuration;
using ControleFinanceiro.Api.Tests.Infrastructure;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Financeiro.Recorrencias;
using ControleFinanceiro.Application.Financeiro.Status;
using ControleFinanceiro.Domain.Financeiro;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ControleFinanceiro.Api.Tests.Financeiro;

public sealed class RecorrenciaCartaoCicloTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private static DateOnly MesAtual => new(DateTime.Today.Year, DateTime.Today.Month, 1);

    private static object Payload(FinancialFixtureSeed.FixtureIds f, bool recorrente, int dia = 5) => new
    {
        dataEmissao = MesAtual.AddDays(dia - 1), dataCompra = MesAtual.AddDays(dia - 1),
        dataVencimento = MesAtual.AddDays(dia - 1), recebedorId = f.RecebedorId,
        formaPagamentoId = f.FormaPagamentoCartaoId, cartaoId = f.CartaoId,
        valorOriginal = 100m, quantidadeParcelas = 1, descricao = "Assinatura cartão",
        rateios = new[] { new { contaGerencialId = f.ContaGerencialDespesaId, valor = 100m } },
        recorrencia = recorrente ? new {
            tipoPeriodicidade = "Mensal", tipoDia = "DiaFixo", diaOrdemMensal = dia,
            dataInicio = MesAtual.AddDays(dia - 1), permiteEdicaoOcorrenciaIndividual = true
        } : null
    };

    private IServiceScope Scope()
    {
        var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IAppDbContext>().DefinirFamiliaCorrente(
            scope.ServiceProvider.GetRequiredService<IOptions<AuthOptions>>().Value.DevelopmentFamiliaId);
        return scope;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MarcarCartaoRecorrente_GeraSeisFuturasESemDuplicarMesOriginal(bool editar)
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var f = await FinancialFixtureSeed.CreateAsync(client);
        var response = await client.PostAsJsonAsync("/api/v1/contas-pagar", Payload(f, !editar));
        response.EnsureSuccessStatusCode();
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        if (editar)
            (await client.PutAsJsonAsync($"/api/v1/contas-pagar/{id}", Payload(f, true))).EnsureSuccessStatusCode();

        using var scope = Scope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var contas = await db.ContasPagar.OrderBy(x => x.DataVencimento).ToArrayAsync();
        contas.Should().HaveCount(7);
        contas.Select(x => x.DataVencimento).Should().Equal(
            Enumerable.Range(0, 7).Select(i => MesAtual.AddMonths(i).AddDays(19)));
        contas[0].Id.Should().Be(id);
        contas[0].StatusContaId.Should().Be(StatusConta.EmFaturaId);
        contas.Skip(1).Should().OnlyContain(x => x.StatusContaId == StatusConta.EmFaturaId);
        contas.Skip(1).Should().OnlyContain(x => x.DataCompra.HasValue);
        var result = await scope.ServiceProvider.GetRequiredService<RecorrenciaAppService>()
            .GerarOcorrenciasRecorrentesNoMesAsync(MesAtual, default);
        (await db.ContasPagar.CountAsync()).Should().Be(7);
        result.Erros.Should().Be(0);
        var faturas = await client.GetFromJsonAsync<JsonElement>("/api/v1/faturas");
        var itens = faturas.GetProperty("items").EnumerateArray().ToArray();
        itens.Should().HaveCount(7);
        foreach (var fatura in itens)
        {
            fatura.GetProperty("quantidadeItens").GetInt32().Should().Be(1);
            fatura.GetProperty("valorTotal").GetDecimal().Should().Be(100m);
            var detalhe = await client.GetFromJsonAsync<JsonElement>($"/api/v1/faturas/{fatura.GetProperty("id").GetGuid()}");
            var item = detalhe.GetProperty("itens").EnumerateArray().Single();
            contas.Select(x => x.Id).Should().Contain(item.GetProperty("contaPagarId").GetGuid());
        }

    }

    [Fact]
    public async Task ViradaDoMes_PromoveMesmaContaDeCartaoParaEmFatura()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var f = await FinancialFixtureSeed.CreateAsync(client);
        (await client.PostAsJsonAsync("/api/v1/contas-pagar", Payload(f, true))).EnsureSuccessStatusCode();
        using var scope = Scope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var futura = await db.ContasPagar.FirstAsync(x => x.DataVencimento > MesAtual.AddMonths(1));
        var id = futura.Id;
        await db.ContasPagar.Where(x => x.Id == id).ExecuteUpdateAsync(s =>
            s.SetProperty(x => x.DataVencimento, MesAtual.AddDays(19)));
        await scope.ServiceProvider.GetRequiredService<TransicaoStatusFuturoService>()
            .TransicionarFuturoParaPendenteAsync(default);
        var status = await db.ContasPagar.AsNoTracking().Where(x => x.Id == id).Select(x => x.StatusContaId).SingleAsync();
        status.Should().Be(StatusConta.EmFaturaId);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RenovacaoMensal_PreservaIdsEAcrescentaSomenteCaudaDeSeisMeses(bool gerarPrimeiro)
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var f = await FinancialFixtureSeed.CreateAsync(client);
        (await client.PostAsJsonAsync("/api/v1/contas-pagar", Payload(f, true))).EnsureSuccessStatusCode();
        for (var mes = 1; mes <= 7; mes++)
        {
            using var scope = Scope();
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var referencia = MesAtual.AddMonths(mes);
            var fimMes = referencia.AddMonths(1).AddDays(-1);
            var idDoMes = await db.ContasPagar.Where(x => x.DataVencimento >= referencia && x.DataVencimento <= fimMes)
                .Select(x => x.Id).SingleAsync();
            var gerador = scope.ServiceProvider.GetRequiredService<RecorrenciaAppService>();
            var transicao = scope.ServiceProvider.GetRequiredService<TransicaoStatusFuturoService>();
            if (!gerarPrimeiro) await transicao.TransicionarFuturoParaPendenteAsync(default, referencia);
            var resultado = await gerador.GerarOcorrenciasRecorrentesNoMesAsync(referencia, default);
            if (gerarPrimeiro) await transicao.TransicionarFuturoParaPendenteAsync(default, referencia);
            resultado.OcorrenciasGeradas.Should().Be(1);
            resultado.Erros.Should().Be(0);
            var contas = await db.ContasPagar.AsNoTracking().OrderBy(x => x.DataVencimento).ToArrayAsync();
            contas.Single(x => x.Id == idDoMes).StatusContaId.Should().Be(StatusConta.EmFaturaId);
            contas.Where(x => x.DataVencimento > fimMes).Should().HaveCount(6);
            contas.Last().DataVencimento.Should().Be(referencia.AddMonths(6).AddDays(19));
            contas.Select(x => (x.DataVencimento.Year, x.DataVencimento.Month)).Should().OnlyHaveUniqueItems();
            (await gerador.GerarOcorrenciasRecorrentesNoMesAsync(referencia, default)).OcorrenciasGeradas.Should().Be(0);
            (await transicao.TransicionarFuturoParaPendenteAsync(default, referencia)).Should().Be(0);
        }
    }

    [Fact]
    public async Task CompraAposFechamento_OcorrenciasPermanecemEmFatura()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var f = await FinancialFixtureSeed.CreateAsync(client);
        (await client.PostAsJsonAsync("/api/v1/contas-pagar", Payload(f, true, 25))).EnsureSuccessStatusCode();
        using var scope = Scope();
        var contas = await scope.ServiceProvider.GetRequiredService<IAppDbContext>().ContasPagar
            .OrderBy(x => x.DataVencimento).ToArrayAsync();
        contas.Should().HaveCount(6);
        contas.Should().OnlyContain(x => x.StatusContaId == StatusConta.EmFaturaId);
        contas.First().DataVencimento.Should().Be(MesAtual.AddMonths(1).AddDays(19));
        contas.Last().DataVencimento.Should().Be(MesAtual.AddMonths(6).AddDays(19));
        contas.Skip(1).Select(x => x.DataCompra).Should().Equal(
            Enumerable.Range(1, 5).Select(i => (DateOnly?)MesAtual.AddMonths(i).AddDays(24)));
    }

    [Fact]
    public async Task GeracaoManual_RespeitaHorizonteDeSeisMesesDoCartao()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var f = await FinancialFixtureSeed.CreateAsync(client);
        var response = await client.PostAsJsonAsync("/api/v1/contas-pagar", Payload(f, true));
        response.EnsureSuccessStatusCode();
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        (await client.PostAsJsonAsync($"/api/v1/contas-pagar/{id}/gerar-ocorrencias",
            new { ateData = MesAtual.AddYears(2) })).EnsureSuccessStatusCode();
        using var scope = Scope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        (await db.ContasPagar.CountAsync()).Should().Be(7);
    }

    [Fact]
    public async Task Encerrar_ExigePausaENaoPermiteRetomada()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var f = await FinancialFixtureSeed.CreateAsync(client);
        (await client.PostAsJsonAsync("/api/v1/contas-pagar", Payload(f, true))).EnsureSuccessStatusCode();
        using var scope = Scope();
        var regraId = await scope.ServiceProvider.GetRequiredService<IAppDbContext>().RegrasRecorrencia.Select(x => x.Id).SingleAsync();
        var url = $"/api/v1/recorrencias/{regraId}";
        (await client.PostAsJsonAsync(url + "/encerrar", new {})).IsSuccessStatusCode.Should().BeFalse();
        (await client.PostAsJsonAsync(url + "/pausar", new {})).EnsureSuccessStatusCode();
        var response = await client.PostAsJsonAsync(url + "/encerrar", new {});
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("encerrada").GetBoolean().Should().BeTrue();
        (await client.PostAsJsonAsync(url + "/retomar", new {})).IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task DataFim_EncerraAutomaticamenteInclusivePausada()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var f = await FinancialFixtureSeed.CreateAsync(client);
        var payload = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(Payload(f, true)))!;
        payload["recorrencia"]!["dataFim"] = MesAtual.AddMonths(2).AddDays(4).ToString("yyyy-MM-dd");
        (await client.PostAsJsonAsync("/api/v1/contas-pagar", payload)).EnsureSuccessStatusCode();
        using var scope = Scope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        (await db.ContasPagar.CountAsync()).Should().Be(3);
        var regraId = await db.RegrasRecorrencia.Select(x => x.Id).SingleAsync();
        (await client.PostAsJsonAsync($"/api/v1/recorrencias/{regraId}/pausar", new {})).EnsureSuccessStatusCode();
        await scope.ServiceProvider.GetRequiredService<RecorrenciaAppService>()
            .GerarOcorrenciasRecorrentesNoMesAsync(MesAtual.AddMonths(2).AddDays(4), default);
        var json = await client.GetFromJsonAsync<JsonElement>($"/api/v1/recorrencias/{regraId}");
        json.GetProperty("encerrada").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Retomar_RestauraSomenteCanceladasPelaPausa_PreservandoIds()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var f = await FinancialFixtureSeed.CreateAsync(client);
        var payload = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(Payload(f, true)))!;
        payload["cartaoId"] = null;
        payload["formaPagamentoId"] = f.FormaPagamentoManualId.ToString();
        (await client.PostAsJsonAsync("/api/v1/contas-pagar", payload)).EnsureSuccessStatusCode();
        using var scope = Scope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var futuras = await db.ContasPagar.AsNoTracking().Where(x => x.StatusContaId == StatusConta.FuturoId).OrderBy(x => x.DataVencimento).ToArrayAsync();
        var manualId = futuras[0].Id;
        await db.ContasPagar.Where(x => x.Id == manualId).ExecuteUpdateAsync(u => u.SetProperty(x => x.StatusContaId, StatusConta.CanceladaId));
        var regraId = await db.RegrasRecorrencia.Select(x => x.Id).SingleAsync();
        var url = $"/api/v1/recorrencias/{regraId}";
        (await client.PostAsJsonAsync(url + "/pausar", new {})).EnsureSuccessStatusCode();
        (await db.ContasPagar.CountAsync(x => x.StatusContaId == StatusConta.CanceladaId)).Should().Be(6);
        (await client.PostAsJsonAsync(url + "/retomar", new {})).EnsureSuccessStatusCode();
        var restauradas = await db.ContasPagar.AsNoTracking().ToArrayAsync();
        restauradas.Should().HaveCount(7);
        restauradas.Single(x => x.Id == manualId).StatusContaId.Should().Be(StatusConta.CanceladaId);
        foreach (var conta in futuras.Skip(1))
            restauradas.Single(x => x.Id == conta.Id).StatusContaId.Should().Be(StatusConta.FuturoId);
    }
}
