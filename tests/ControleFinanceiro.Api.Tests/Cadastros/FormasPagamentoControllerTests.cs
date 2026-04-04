using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Cadastros;

public sealed class FormasPagamentoControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    [Fact]
    public async Task PostEPut_DevemPersistirFormaPagamento()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/v1/formas-pagamento", new
        {
            nome = "Pix principal",
            tipo = "Pix",
            ehCartao = false,
            baixarAutomaticamente = true,
            ativo = true
        });

        var created = await createResponse.Content.ReadFromJsonAsync<FormaPagamentoResponse>();
        var updateResponse = await client.PutAsJsonAsync($"/api/v1/formas-pagamento/{created!.Id}", new
        {
            nome = "Pix empresa",
            tipo = "Pix",
            ehCartao = false,
            baixarAutomaticamente = false,
            ativo = true
        });

        var updated = await updateResponse.Content.ReadFromJsonAsync<FormaPagamentoResponse>();

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        updated.Should().NotBeNull();
        updated!.Nome.Should().Be("Pix empresa");
        updated.BaixarAutomaticamente.Should().BeFalse();
    }

    private sealed record FormaPagamentoResponse(
        Guid Id,
        string Nome,
        string Tipo,
        bool EhCartao,
        bool BaixarAutomaticamente,
        bool Ativo);
}
