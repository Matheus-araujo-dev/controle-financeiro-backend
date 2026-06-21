using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Financeiro;

public sealed class ContasPagarFluxoTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private sealed record ContaResumo(Guid Id, string StatusCodigo);

    private static async Task<Guid> CriarContaPagarAsync(HttpClient client, FinancialFixtureSeed.FixtureIds fixture, decimal valor = 150m)
    {
        var response = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = "2026-04-04",
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-04-25",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = valor,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Despesa fluxo",
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialDespesaId, valor } }
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<ContaResumo>();
        return created!.Id;
    }

    [Fact]
    public async Task LiquidarEEstornar_DeveVoltarStatusParaPendente()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var id = await CriarContaPagarAsync(client, fixture);

        var liquidar = await client.PostAsJsonAsync($"/api/v1/contas-pagar/{id}/liquidar", new
        {
            dataLiquidacao = "2026-04-08",
            contaBancariaId = fixture.ContaBancariaId,
            valorLiquidacao = 150m,
            atualizarValorConta = true
        });
        liquidar.StatusCode.Should().Be(HttpStatusCode.OK);
        (await liquidar.Content.ReadFromJsonAsync<ContaResumo>())!.StatusCodigo.Should().Be("LIQUIDADA");

        var estornar = await client.PostAsync($"/api/v1/contas-pagar/{id}/estornar", content: null);
        estornar.StatusCode.Should().Be(HttpStatusCode.OK);
        (await estornar.Content.ReadFromJsonAsync<ContaResumo>())!.StatusCodigo.Should().Be("PENDENTE");
    }

    [Fact]
    public async Task Cancelar_DeveMarcarComoCancelada()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var id = await CriarContaPagarAsync(client, fixture);

        var cancelar = await client.PostAsync($"/api/v1/contas-pagar/{id}/cancelar", content: null);

        cancelar.StatusCode.Should().Be(HttpStatusCode.OK);
        (await cancelar.Content.ReadFromJsonAsync<ContaResumo>())!.StatusCodigo.Should().Be("CANCELADA");
    }

    [Fact]
    public async Task ObterPorId_Inexistente_DeveRetornar404()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var resposta = await client.GetAsync($"/api/v1/contas-pagar/{Guid.NewGuid()}");

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Estornar_Inexistente_DeveRetornar404()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var resposta = await client.PostAsync($"/api/v1/contas-pagar/{Guid.NewGuid()}/estornar", content: null);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
