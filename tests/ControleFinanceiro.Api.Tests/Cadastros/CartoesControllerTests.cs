using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using ControleFinanceiro.Api.Tests.Financeiro;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Cadastros;

public sealed class CartoesControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    [Fact]
    public async Task Post_QuandoPayloadValido_DeveCriarCartao()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/v1/cartoes", new
        {
            nome = "Cartao corporativo",
            bandeira = "Visa",
            numeroFinal = "1234",
            diaFechamentoFatura = 8,
            diaVencimentoFatura = 15,
            limiteCredito = 10000m,
            ativo = true
        });

        var created = await createResponse.Content.ReadFromJsonAsync<CartaoResponse>();

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        created.Should().NotBeNull();
        created!.NumeroFinal.Should().Be("1234");
    }

    [Fact]
    public async Task Get_QuandoContaPossuirLimiteCompartilhado_DeveExibirLimiteEfetivoAgregadoNosCartoes()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var atualizarConta = await client.PutAsJsonAsync($"/api/v1/contas-bancarias/{fixture.ContaBancariaId}", new
        {
            nome = "Conta operacional",
            banco = "Banco Exemplo",
            agencia = "0001",
            numeroConta = "12345-6",
            tipoConta = "Corrente",
            saldoInicial = 1000m,
            dataSaldoInicial = "2026-04-01",
            limiteCartoesCompartilhado = 6000m,
            ativo = true
        });

        atualizarConta.EnsureSuccessStatusCode();

        var createSecondCard = await client.PostAsJsonAsync("/api/v1/cartoes", new
        {
            nome = "Cartao adicional",
            bandeira = "Mastercard",
            numeroFinal = "5454",
            diaFechamentoFatura = 10,
            diaVencimentoFatura = 20,
            contaBancariaPagamentoPadraoId = fixture.ContaBancariaId,
            limiteCredito = 2000m,
            ativo = true
        });

        createSecondCard.EnsureSuccessStatusCode();
        var segundoCartao = await createSecondCard.Content.ReadFromJsonAsync<CartaoResponse>();

        await CriarCompraCartaoAsync(client, fixture, fixture.CartaoId, 100m, "Compra no cartao principal");
        await CriarCompraCartaoAsync(client, fixture, segundoCartao!.Id, 250m, "Compra no cartao adicional");

        var listResponse = await client.GetFromJsonAsync<PagedResponse<CartaoResponse>>("/api/v1/cartoes");
        var contaDetalhe = await client.GetFromJsonAsync<ContaBancariaResponse>($"/api/v1/contas-bancarias/{fixture.ContaBancariaId}");

        listResponse.Should().NotBeNull();
        contaDetalhe.Should().NotBeNull();
        contaDetalhe!.LimiteCartoesCompartilhado.Should().Be(6000m);
        contaDetalhe.LimiteCartoesComprometido.Should().Be(350m);
        contaDetalhe.LimiteCartoesDisponivel.Should().Be(5650m);

        listResponse!.Items.Should().Contain(item =>
            item.Id == fixture.CartaoId &&
            item.UsaLimiteCompartilhado &&
            item.LimiteEfetivo == 6000m &&
            item.LimiteComprometido == 350m &&
            item.LimiteDisponivel == 5650m);

        listResponse.Items.Should().Contain(item =>
            item.Id == segundoCartao.Id &&
            item.UsaLimiteCompartilhado &&
            item.LimiteEfetivo == 6000m &&
            item.LimiteComprometido == 350m &&
            item.LimiteDisponivel == 5650m);
    }

    [Fact]
    public async Task Post_QuandoNumeroFinalInvalido_DeveRetornarErroDeValidacao()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/cartoes", new
        {
            nome = "Cartao corporativo",
            bandeira = "Visa",
            numeroFinal = "12A4",
            diaFechamentoFatura = 8,
            diaVencimentoFatura = 15,
            ativo = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private sealed record CartaoResponse(
        Guid Id,
        string Nome,
        string Bandeira,
        string NumeroFinal,
        int DiaFechamentoFatura,
        int DiaVencimentoFatura,
        Guid? ContaBancariaPagamentoPadraoId,
        decimal? LimiteCredito,
        bool UsaLimiteCompartilhado,
        decimal? LimiteEfetivo,
        decimal LimiteComprometido,
        decimal? LimiteDisponivel,
        bool Ativo);

    private sealed record ContaBancariaResponse(
        Guid Id,
        string Nome,
        string Banco,
        string? Agencia,
        string? NumeroConta,
        string? TipoConta,
        decimal SaldoInicial,
        string DataSaldoInicial,
        decimal? LimiteCartoesCompartilhado,
        decimal LimiteCartoesComprometido,
        decimal? LimiteCartoesDisponivel,
        bool Ativo);

    private sealed record PagedResponse<T>(IReadOnlyCollection<T> Items, int Page, int PageSize, int TotalItems, int TotalPages);

    private static async Task CriarCompraCartaoAsync(
        HttpClient client,
        FinancialFixtureSeed.FixtureIds fixture,
        Guid cartaoId,
        decimal valor,
        string descricao)
    {
        var response = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            numeroDocumento = "CARTAO-SHARED",
            dataEmissao = "2026-04-05",
            responsavelCompraId = fixture.ResponsavelId,
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-04-20",
            formaPagamentoId = fixture.FormaPagamentoCartaoId,
            cartaoId,
            contaBancariaId = (string?)null,
            valorOriginal = valor,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao,
            observacao = "Compra compartilhando limite",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialDespesaId, valor }
            }
        });

        response.EnsureSuccessStatusCode();
    }
}
