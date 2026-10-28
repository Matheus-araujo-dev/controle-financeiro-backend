using System.Net.Http.Json;
using System.Text.Json;
using ControleFinanceiro.Api.Configuration;
using ControleFinanceiro.Api.Tests.Infrastructure;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Financeiro.Recorrencias;
using ControleFinanceiro.Domain.Financeiro;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ControleFinanceiro.Api.Tests.Financeiro;

/// <summary>
/// Recorrências pausadas ou encerradas não podem gerar contas a pagar/receber quando
/// o gerador automático (worker / POST gerar-ocorrencias) roda, em nenhum mês posterior.
/// </summary>
public sealed class RecorrenciaInativaNaoGeraTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private static DateOnly MesAtual => new(DateTime.Today.Year, DateTime.Today.Month, 1);

    public enum Tipo { Pagar, PagarCartao, Receber }

    private IServiceScope Scope()
    {
        var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IAppDbContext>().DefinirFamiliaCorrente(
            scope.ServiceProvider.GetRequiredService<IOptions<AuthOptions>>().Value.DevelopmentFamiliaId);
        return scope;
    }

    private static async Task<Guid> CriarAsync(HttpClient client, FinancialFixtureSeed.FixtureIds f, Tipo tipo)
    {
        var data = MesAtual.AddDays(4);
        var recorrencia = new
        {
            tipoPeriodicidade = "Mensal", tipoDia = "DiaFixo", diaOrdemMensal = 5,
            dataInicio = data, permiteEdicaoOcorrenciaIndividual = true
        };
        object payload = tipo switch
        {
            Tipo.Receber => new
            {
                dataEmissao = data, dataVencimento = data, recebedorId = f.MembroId, pagadorId = f.PagadorId,
                formaPagamentoId = f.FormaPagamentoManualId, valorOriginal = 100m, quantidadeParcelas = 1,
                descricao = "Receita recorrente",
                rateios = new[] { new { contaGerencialId = f.ContaGerencialReceitaId, valor = 100m } },
                recorrencia
            },
            Tipo.PagarCartao => new
            {
                dataEmissao = data, dataCompra = data, dataVencimento = data, recebedorId = f.RecebedorId,
                formaPagamentoId = f.FormaPagamentoCartaoId, cartaoId = f.CartaoId, valorOriginal = 100m,
                quantidadeParcelas = 1, descricao = "Assinatura cartão",
                rateios = new[] { new { contaGerencialId = f.ContaGerencialDespesaId, valor = 100m } },
                recorrencia
            },
            _ => new
            {
                dataEmissao = data, dataVencimento = data, recebedorId = f.RecebedorId,
                pagadorId = f.MembroId, formaPagamentoId = f.FormaPagamentoManualId,
                valorOriginal = 100m, quantidadeParcelas = 1, descricao = "Aluguel",
                rateios = new[] { new { contaGerencialId = f.ContaGerencialDespesaId, valor = 100m } },
                recorrencia
            }
        };
        var rota = tipo == Tipo.Receber ? "/api/v1/contas-receber" : "/api/v1/contas-pagar";
        var response = await client.PostAsJsonAsync(rota, payload);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("recorrencia").GetProperty("id").GetGuid();
    }

    private static async Task<(int Total, int Abertas)> ContarAsync(IAppDbContext db, Tipo tipo, Guid regraId)
    {
        if (tipo == Tipo.Receber)
        {
            var receber = await db.ContasReceber.AsNoTracking().Where(x => x.RegraRecorrenciaId == regraId)
                .Select(x => x.StatusContaId).ToArrayAsync();
            return (receber.Length, receber.Count(s => s != StatusConta.CanceladaId));
        }
        var pagar = await db.ContasPagar.AsNoTracking().Where(x => x.RegraRecorrenciaId == regraId)
            .Select(x => x.StatusContaId).ToArrayAsync();
        return (pagar.Length, pagar.Count(s => s != StatusConta.CanceladaId));
    }

    [Theory]
    [InlineData(Tipo.Pagar, false)]
    [InlineData(Tipo.Pagar, true)]
    [InlineData(Tipo.PagarCartao, false)]
    [InlineData(Tipo.PagarCartao, true)]
    [InlineData(Tipo.Receber, false)]
    [InlineData(Tipo.Receber, true)]
    public async Task Gerador_RecorrenciaPausadaOuEncerrada_NaoGeraContas(Tipo tipo, bool encerrar)
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var f = await FinancialFixtureSeed.CreateAsync(client);
        var regraId = await CriarAsync(client, f, tipo);

        (await client.PostAsync($"/api/v1/recorrencias/{regraId}/pausar", null)).EnsureSuccessStatusCode();
        if (encerrar)
            (await client.PostAsync($"/api/v1/recorrencias/{regraId}/encerrar", null)).EnsureSuccessStatusCode();

        int totalAntes, abertasAntes;
        using (var scope = Scope())
            (totalAntes, abertasAntes) = await ContarAsync(scope.ServiceProvider.GetRequiredService<IAppDbContext>(), tipo, regraId);

        (await client.PostAsync("/api/v1/recorrencias/gerar-ocorrencias", null)).EnsureSuccessStatusCode();
        for (var mes = 0; mes <= 12; mes++)
        {
            using var scope = Scope();
            var resultado = await scope.ServiceProvider.GetRequiredService<RecorrenciaAppService>()
                .GerarOcorrenciasRecorrentesNoMesAsync(MesAtual.AddMonths(mes), default);
            resultado.OcorrenciasGeradas.Should().Be(0);
        }

        using (var scope = Scope())
        {
            var (total, abertas) = await ContarAsync(scope.ServiceProvider.GetRequiredService<IAppDbContext>(), tipo, regraId);
            total.Should().Be(totalAntes);
            abertas.Should().Be(abertasAntes);
        }
    }

    private static async Task<Guid[]> FuturasAbertasAsync(IAppDbContext db, Tipo tipo, Guid regraId)
    {
        var fimMes = MesAtual.AddMonths(1).AddDays(-1);
        return tipo == Tipo.Receber
            ? await db.ContasReceber.AsNoTracking()
                .Where(x => x.RegraRecorrenciaId == regraId && x.DataVencimento > fimMes && x.StatusContaId != StatusConta.CanceladaId)
                .Select(x => x.StatusContaId).ToArrayAsync()
            : await db.ContasPagar.AsNoTracking()
                .Where(x => x.RegraRecorrenciaId == regraId && x.DataVencimento > fimMes && x.StatusContaId != StatusConta.CanceladaId)
                .Select(x => x.StatusContaId).ToArrayAsync();
    }

    [Theory]
    [InlineData(Tipo.Pagar)]
    [InlineData(Tipo.PagarCartao)]
    [InlineData(Tipo.Receber)]
    public async Task Pausar_CancelaOcorrenciasDeMesesFuturos_ERetomarRestaura(Tipo tipo)
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var f = await FinancialFixtureSeed.CreateAsync(client);
        var regraId = await CriarAsync(client, f, tipo);
        Guid[] antes;
        using (var scope = Scope())
            antes = await FuturasAbertasAsync(scope.ServiceProvider.GetRequiredService<IAppDbContext>(), tipo, regraId);
        antes.Should().HaveCount(6);

        (await client.PostAsync($"/api/v1/recorrencias/{regraId}/pausar", null)).EnsureSuccessStatusCode();
        using (var scope = Scope())
            (await FuturasAbertasAsync(scope.ServiceProvider.GetRequiredService<IAppDbContext>(), tipo, regraId))
                .Should().BeEmpty();

        (await client.PostAsync($"/api/v1/recorrencias/{regraId}/retomar", null)).EnsureSuccessStatusCode();
        using (var scope = Scope())
            (await FuturasAbertasAsync(scope.ServiceProvider.GetRequiredService<IAppDbContext>(), tipo, regraId))
                .Should().BeEquivalentTo(antes);
    }

    [Theory]
    [InlineData(Tipo.Pagar)]
    [InlineData(Tipo.PagarCartao)]
    [InlineData(Tipo.Receber)]
    public async Task CancelarContaPausandoRecorrencia_CancelaOcorrenciasDeMesesFuturos(Tipo tipo)
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var f = await FinancialFixtureSeed.CreateAsync(client);
        var regraId = await CriarAsync(client, f, tipo);
        Guid contaOrigemId;
        using (var scope = Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            contaOrigemId = tipo == Tipo.Receber
                ? await db.ContasReceber.Where(x => x.RegraRecorrenciaId == regraId && x.Origem != OrigemLancamento.Recorrencia).Select(x => x.Id).SingleAsync()
                : await db.ContasPagar.Where(x => x.RegraRecorrenciaId == regraId && x.Origem != OrigemLancamento.Recorrencia).Select(x => x.Id).SingleAsync();
        }

        var rota = tipo == Tipo.Receber ? "contas-receber" : "contas-pagar";
        (await client.PostAsJsonAsync($"/api/v1/{rota}/{contaOrigemId}/cancelar", new { pausarRecorrenciaRelacionada = true }))
            .EnsureSuccessStatusCode();

        using (var scope = Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            (await db.RegrasRecorrencia.AsNoTracking().SingleAsync(x => x.Id == regraId)).Ativa.Should().BeFalse();
            (await FuturasAbertasAsync(db, tipo, regraId)).Should().BeEmpty();
        }
    }
}
