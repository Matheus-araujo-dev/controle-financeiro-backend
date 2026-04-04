using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Cadastros;

public sealed class ContasBancariasControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    [Fact]
    public async Task Post_DeveCriarContaBancariaEPermitirConsultaDetalhada()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/v1/contas-bancarias", new
        {
            nome = "Conta principal",
            banco = "Banco Exemplo",
            agencia = "0001",
            numeroConta = "12345-6",
            tipoConta = "Corrente",
            saldoInicial = 1500.75m,
            dataSaldoInicial = "2026-04-01",
            ativo = true
        });

        var created = await createResponse.Content.ReadFromJsonAsync<ContaBancariaResponse>();
        var detail = await client.GetFromJsonAsync<ContaBancariaResponse>($"/api/v1/contas-bancarias/{created!.Id}");

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        detail.Should().NotBeNull();
        detail!.Banco.Should().Be("Banco Exemplo");
        detail.SaldoInicial.Should().Be(1500.75m);
    }

    private sealed record ContaBancariaResponse(
        Guid Id,
        string Nome,
        string Banco,
        string? Agencia,
        string? NumeroConta,
        string? TipoConta,
        decimal SaldoInicial,
        string DataSaldoInicial,
        bool Ativo);
}
