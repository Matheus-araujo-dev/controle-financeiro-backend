using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Financeiro;

public sealed class ContasReceberFluxoTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private sealed record ContaResumo(Guid Id, string StatusCodigo, string StatusNome);

    private static async Task<Guid> CriarContaReceberAsync(HttpClient client, FinancialFixtureSeed.FixtureIds fixture, decimal valor = 200m)
    {
        var response = await client.PostAsJsonAsync("/api/v1/contas-receber", new
        {
            dataEmissao = "2026-04-04",
            pagadorId = fixture.PagadorId,
            dataVencimento = "2026-04-25",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = valor,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Receita fluxo",
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialReceitaId, valor } }
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
        var id = await CriarContaReceberAsync(client, fixture);

        var liquidar = await client.PostAsJsonAsync($"/api/v1/contas-receber/{id}/liquidar", new
        {
            dataLiquidacao = "2026-04-08",
            contaBancariaId = fixture.ContaBancariaId,
            valorLiquidacao = 200m,
            atualizarValorConta = true
        });
        liquidar.StatusCode.Should().Be(HttpStatusCode.OK);
        var liquidada = await liquidar.Content.ReadFromJsonAsync<ContaResumo>();
        liquidada!.StatusCodigo.Should().Be("LIQUIDADA");

        var estornar = await client.PostAsync($"/api/v1/contas-receber/{id}/estornar", content: null);
        estornar.StatusCode.Should().Be(HttpStatusCode.OK);
        var estornada = await estornar.Content.ReadFromJsonAsync<ContaResumo>();
        estornada!.StatusCodigo.Should().Be("PENDENTE");

        // Movimentação de entrada foi removida pelo estorno
        var movs = await client.GetFromJsonAsync<PagedResponse<MovimentacaoResumoResponse>>(
            "/api/v1/movimentacoes?search=Receita fluxo");
        movs!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Cancelar_DeveMarcarComoCancelada()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var id = await CriarContaReceberAsync(client, fixture);

        var cancelar = await client.PostAsync($"/api/v1/contas-receber/{id}/cancelar", content: null);

        cancelar.StatusCode.Should().Be(HttpStatusCode.OK);
        var cancelada = await cancelar.Content.ReadFromJsonAsync<ContaResumo>();
        cancelada!.StatusCodigo.Should().Be("CANCELADA");
    }

    [Fact]
    public async Task ObterPorId_DeveRetornarDetalhe_EInexistente404()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var id = await CriarContaReceberAsync(client, fixture);

        var detalhe = await client.GetFromJsonAsync<ContaResumo>($"/api/v1/contas-receber/{id}");
        detalhe!.Id.Should().Be(id);

        var inexistente = await client.GetAsync($"/api/v1/contas-receber/{Guid.NewGuid()}");
        inexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Estornar_Inexistente_DeveRetornar404()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var resposta = await client.PostAsync($"/api/v1/contas-receber/{Guid.NewGuid()}/estornar", content: null);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed record PagedResponse<T>(IReadOnlyCollection<T> Items);
    private sealed record MovimentacaoResumoResponse(Guid Id, decimal Valor);
}
