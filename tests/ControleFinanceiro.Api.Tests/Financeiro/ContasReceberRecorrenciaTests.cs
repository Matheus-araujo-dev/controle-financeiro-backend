using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Financeiro;

public sealed class ContasReceberRecorrenciaTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private sealed record Conta(Guid Id);

    private static async Task<Guid> CriarRecorrenteAsync(HttpClient client, FinancialFixtureSeed.FixtureIds fixture)
    {
        var resp = await client.PostAsJsonAsync("/api/v1/contas-receber", new
        {
            dataEmissao = "2026-04-04",
            pagadorId = fixture.PagadorId,
            dataVencimento = "2026-04-15",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 1200m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Mensalidade recorrente",
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialReceitaId, valor = 1200m } },
            recorrencia = new
            {
                tipoPeriodicidade = "Mensal",
                tipoDia = "DiaFixo",
                diaOrdemMensal = 15,
                dataInicio = (string?)null,
                dataFim = "2026-12-01",
                permiteEdicaoOcorrenciaIndividual = false,
                observacao = (string?)null
            }
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await resp.Content.ReadFromJsonAsync<Conta>())!.Id;
    }

    [Fact]
    public async Task GerarOcorrenciasAlterarFuturasPausarEEncerrar()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var id = await CriarRecorrenteAsync(client, fixture);

        var gerar = await client.PostAsJsonAsync($"/api/v1/contas-receber/{id}/gerar-ocorrencias", new { ateData = "2026-08-01" });
        gerar.StatusCode.Should().Be(HttpStatusCode.OK);

        var alterar = await client.PostAsJsonAsync($"/api/v1/contas-receber/{id}/alterar-futuras", new
        {
            id,
            dataEmissao = "2026-04-04",
            responsavelId = fixture.ResponsavelId,
            pagadorId = fixture.PagadorId,
            dataVencimento = "2026-04-15",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 1300m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Mensalidade reajustada",
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialReceitaId, valor = 1300m } }
        });
        alterar.StatusCode.Should().Be(HttpStatusCode.OK);

        var pausar = await client.PostAsync($"/api/v1/contas-receber/{id}/pausar-recorrencia", content: null);
        pausar.StatusCode.Should().Be(HttpStatusCode.OK);

        var encerrar = await client.PostAsJsonAsync($"/api/v1/contas-receber/{id}/encerrar-recorrencia", new { dataFim = "2026-06-01" });
        encerrar.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EncerrarRecorrencia_Inexistente_DeveRetornar404()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var resp = await client.PostAsJsonAsync($"/api/v1/contas-receber/{Guid.NewGuid()}/encerrar-recorrencia", new { dataFim = "2026-06-01" });

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
