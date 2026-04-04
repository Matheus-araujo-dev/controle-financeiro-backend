using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Financeiro;

public sealed class FaturasControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    [Fact]
    public async Task GetEPostPagar_DeveAgruparComprasExibirItensEGerarSaidaRealDaFatura()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var compraAbrilA = await CriarCompraCartaoAsync(client, fixture, "2026-04-05", 100m, "Compra cartao abril A");
        var compraAbrilB = await CriarCompraCartaoAsync(client, fixture, "2026-04-10", 50m, "Compra cartao abril B");
        await CriarCompraCartaoAsync(client, fixture, "2026-04-11", 75m, "Compra cartao maio");

        var movimentosAntesPagamento = await client.GetFromJsonAsync<PagedResponse<MovimentacaoResumoResponse>>("/api/v1/movimentacoes");
        var faturas = await client.GetFromJsonAsync<PagedResponse<FaturaResumoResponse>>("/api/v1/faturas");

        movimentosAntesPagamento.Should().NotBeNull();
        movimentosAntesPagamento!.Items.Should().Contain(item =>
            item.ContaPagarId == compraAbrilA &&
            item.Tipo == "Saida" &&
            item.Natureza == "Economica" &&
            item.ContaBancariaId == null);
        movimentosAntesPagamento.Items.Should().Contain(item =>
            item.ContaPagarId == compraAbrilB &&
            item.Tipo == "Saida" &&
            item.Natureza == "Economica" &&
            item.ContaBancariaId == null);

        faturas.Should().NotBeNull();
        faturas!.Items.Should().HaveCount(2);

        var faturaAbril = faturas.Items.Single(item => item.Competencia == "2026-04");
        faturaAbril.ValorTotal.Should().Be(150m);
        faturaAbril.QuantidadeItens.Should().Be(2);
        faturaAbril.StatusCodigo.Should().Be("ABERTA");

        var detalheAntesPagamento = await client.GetFromJsonAsync<FaturaDetalheResponse>($"/api/v1/faturas/{faturaAbril.Id}");
        detalheAntesPagamento.Should().NotBeNull();
        detalheAntesPagamento!.Itens.Should().HaveCount(2);
        detalheAntesPagamento.Itens.Should().OnlyContain(item => item.StatusCodigo == "PENDENTE");

        var pagarResponse = await client.PostAsJsonAsync($"/api/v1/faturas/{faturaAbril.Id}/pagar", new
        {
            dataPagamento = "2026-04-20",
            contaBancariaPagamentoId = fixture.ContaBancariaId,
            observacao = "Pagamento da fatura abril"
        });

        var detalheDepoisPagamento = await client.GetFromJsonAsync<FaturaDetalheResponse>($"/api/v1/faturas/{faturaAbril.Id}");
        var movimentosDepoisPagamento = await client.GetFromJsonAsync<PagedResponse<MovimentacaoResumoResponse>>("/api/v1/movimentacoes");

        pagarResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        detalheDepoisPagamento.Should().NotBeNull();
        detalheDepoisPagamento!.StatusCodigo.Should().Be("PAGA");
        detalheDepoisPagamento.DataPagamento.Should().Be(new DateOnly(2026, 4, 20));
        detalheDepoisPagamento.Itens.Should().OnlyContain(item => item.StatusCodigo == "LIQUIDADA");

        movimentosDepoisPagamento.Should().NotBeNull();
        movimentosDepoisPagamento!.Items.Should().ContainSingle(item =>
            item.FaturaCartaoId == faturaAbril.Id &&
            item.Tipo == "Saida" &&
            item.Natureza == "Realizada" &&
            item.ContaBancariaId == fixture.ContaBancariaId &&
            item.Valor == 150m);
    }

    private static async Task<Guid> CriarCompraCartaoAsync(
        HttpClient client,
        FinancialFixtureSeed.FixtureIds fixture,
        string dataEmissao,
        decimal valor,
        string descricao)
    {
        var response = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            numeroDocumento = "CARTAO-01",
            dataEmissao,
            responsavelCompraId = fixture.ResponsavelId,
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-04-20",
            formaPagamentoId = fixture.FormaPagamentoCartaoId,
            cartaoId = fixture.CartaoId,
            contaBancariaId = (string?)null,
            valorOriginal = valor,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao,
            observacao = "Compra via cartao",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialDespesaId, valor }
            }
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ContaDetalheResponse>();
        return payload!.Id;
    }

    private sealed record PagedResponse<T>(IReadOnlyCollection<T> Items, int Page, int PageSize, int TotalItems, int TotalPages);

    private sealed record ContaDetalheResponse(Guid Id);

    private sealed record FaturaResumoResponse(
        Guid Id,
        Guid CartaoId,
        string CartaoNome,
        string Competencia,
        DateOnly DataFechamento,
        DateOnly DataVencimento,
        decimal ValorTotal,
        DateOnly? DataPagamento,
        string StatusCodigo,
        string StatusNome,
        int QuantidadeItens);

    private sealed record FaturaDetalheResponse(
        Guid Id,
        Guid CartaoId,
        string CartaoNome,
        string Competencia,
        DateOnly DataFechamento,
        DateOnly DataVencimento,
        decimal ValorTotal,
        DateOnly? DataPagamento,
        Guid? ContaBancariaPagamentoId,
        string? ContaBancariaPagamentoNome,
        string StatusCodigo,
        string StatusNome,
        string? Observacao,
        IReadOnlyCollection<FaturaItemResponse> Itens);

    private sealed record FaturaItemResponse(
        Guid ContaPagarId,
        string Descricao,
        string RecebedorNome,
        DateOnly DataCompra,
        decimal ValorLiquido,
        string StatusCodigo,
        int NumeroParcela,
        int QuantidadeParcelas);

    private sealed record MovimentacaoResumoResponse(
        Guid Id,
        DateOnly DataMovimentacao,
        string Tipo,
        string Natureza,
        string StatusCodigo,
        decimal Valor,
        Guid? ContaBancariaId,
        Guid? ContaPagarId,
        Guid? ContaReceberId,
        Guid? FaturaCartaoId,
        string? Observacao);
}
