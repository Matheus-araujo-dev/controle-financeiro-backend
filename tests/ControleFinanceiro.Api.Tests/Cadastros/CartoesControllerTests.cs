using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
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
        bool Ativo);
}
