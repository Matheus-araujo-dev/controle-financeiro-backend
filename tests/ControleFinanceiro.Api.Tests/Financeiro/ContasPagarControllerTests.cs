using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Financeiro;

public sealed class ContasPagarControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    [Fact]
    public async Task Post_DeveCriarContaParceladaEListarParcelas()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var createResponse = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            numeroDocumento = "NF-2026-1",
            dataEmissao = "2026-04-04",
            responsavelCompraId = fixture.ResponsavelId,
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-04-20",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            cartaoId = (string?)null,
            contaBancariaId = (string?)null,
            valorOriginal = 100.00m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0.01m,
            quantidadeParcelas = 3,
            descricao = "Servico parcelado",
            observacao = "Observacao de teste",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 60.01m },
                new { contaGerencialId = fixture.ContaGerencialAdministrativaId, valor = 40m }
            }
        });

        var created = await createResponse.Content.ReadFromJsonAsync<ContaDetalheResponse>();
        var listResponse = await client.GetFromJsonAsync<PagedResponse<ContaResumoResponse>>("/api/v1/contas-pagar?search=Servico");

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        created.Should().NotBeNull();
        created!.NumeroParcela.Should().Be(1);
        created.QuantidadeParcelas.Should().Be(3);
        listResponse.Should().NotBeNull();
        listResponse!.Items.Should().HaveCount(3);
        listResponse.Items.Sum(item => item.ValorLiquido).Should().Be(100.01m);
    }

    [Fact]
    public async Task Post_QuandoFormaPagamentoBaixaAutomaticamenteSemContaBancaria_DeveRetornarBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var response = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = "2026-04-04",
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-04-20",
            formaPagamentoId = fixture.FormaPagamentoAutoId,
            valorOriginal = 150m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Baixa automatica sem conta",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 150m }
            }
        });

        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error.Should().NotBeNull();
        error!.Errors.Should().ContainKey("ContaBancariaId");
    }

    [Fact]
    public async Task PostLiquidar_DeveGerarMovimentacaoDeSaida()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var createResponse = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = "2026-04-04",
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-04-20",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 120m,
            valorDesconto = 20m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Conta para liquidar",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 100m }
            }
        });

        var created = await createResponse.Content.ReadFromJsonAsync<ContaDetalheResponse>();

        var liquidarResponse = await client.PostAsJsonAsync($"/api/v1/contas-pagar/{created!.Id}/liquidar", new
        {
            dataLiquidacao = "2026-04-06",
            contaBancariaId = fixture.ContaBancariaId
        });

        var movimento = await client.GetFromJsonAsync<PagedResponse<MovimentacaoResumoResponse>>("/api/v1/movimentacoes?search=Conta para liquidar");

        liquidarResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        movimento.Should().NotBeNull();
        movimento!.Items.Should().ContainSingle(item =>
            item.Tipo == "Saida" &&
            item.Natureza == "Realizada" &&
            item.ContaPagarId == created.Id &&
            item.Valor == 100m);
    }

    private sealed record PagedResponse<T>(IReadOnlyCollection<T> Items, int Page, int PageSize, int TotalItems, int TotalPages);

    private sealed record ApiErrorResponse(string Code, string Message, IReadOnlyDictionary<string, string[]> Errors, string TraceId);

    private sealed record ContaResumoResponse(Guid Id, string Descricao, decimal ValorLiquido, int QuantidadeParcelas, int NumeroParcela);

    private sealed record ContaDetalheResponse(Guid Id, string Descricao, decimal ValorLiquido, int QuantidadeParcelas, int NumeroParcela);

    private sealed record MovimentacaoResumoResponse(
        Guid Id,
        DateOnly DataMovimentacao,
        string Tipo,
        string Natureza,
        string StatusCodigo,
        decimal Valor,
        Guid? ContaPagarId,
        Guid? ContaReceberId,
        string? Observacao);
}
