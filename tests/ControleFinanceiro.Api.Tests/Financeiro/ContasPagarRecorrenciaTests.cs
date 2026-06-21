using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Financeiro;

public sealed class ContasPagarRecorrenciaTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private sealed record Conta(Guid Id);

    private static async Task<Guid> CriarRecorrenteAsync(HttpClient client, FinancialFixtureSeed.FixtureIds fixture)
    {
        var resp = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = "2026-04-04",
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-04-10",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 99.90m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Assinatura recorrente",
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 99.90m } },
            recorrencia = new
            {
                tipoPeriodicidade = "Mensal",
                tipoDia = "DiaFixo",
                diaOrdemMensal = 10,
                dataInicio = (string?)null,
                dataFim = "2026-12-01",
                permiteEdicaoOcorrenciaIndividual = true,
                observacao = (string?)null
            }
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await resp.Content.ReadFromJsonAsync<Conta>())!.Id;
    }

    [Fact]
    public async Task GerarOcorrenciasPausarEEncerrar()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var id = await CriarRecorrenteAsync(client, fixture);

        var gerar = await client.PostAsJsonAsync($"/api/v1/contas-pagar/{id}/gerar-ocorrencias", new { ateData = "2026-08-01" });
        gerar.StatusCode.Should().Be(HttpStatusCode.OK);

        var pausar = await client.PostAsync($"/api/v1/contas-pagar/{id}/pausar-recorrencia", content: null);
        pausar.StatusCode.Should().Be(HttpStatusCode.OK);

        var encerrar = await client.PostAsJsonAsync($"/api/v1/contas-pagar/{id}/encerrar-recorrencia", new { dataFim = "2026-06-01" });
        encerrar.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AlterarFuturas_DeveReajustarAsOcorrenciasFuturas()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var id = await CriarRecorrenteAsync(client, fixture);

        await client.PostAsJsonAsync($"/api/v1/contas-pagar/{id}/gerar-ocorrencias", new { ateData = "2026-08-01" });

        var alterar = await client.PostAsJsonAsync($"/api/v1/contas-pagar/{id}/alterar-futuras", new
        {
            id,
            dataEmissao = "2026-04-04",
            responsavelCompraId = (Guid?)null,
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-04-10",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 120m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Assinatura reajustada",
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 120m } }
        });

        alterar.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GerarOcorrencias_Inexistente_DeveRetornar404()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var resp = await client.PostAsJsonAsync($"/api/v1/contas-pagar/{Guid.NewGuid()}/gerar-ocorrencias", new { ateData = "2026-08-01" });

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PausarRecorrencia_Inexistente_DeveRetornar404()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var resp = await client.PostAsync($"/api/v1/contas-pagar/{Guid.NewGuid()}/pausar-recorrencia", content: null);

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
