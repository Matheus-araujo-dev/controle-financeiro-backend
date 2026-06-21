using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Cadastros;

public sealed class FormasPagamentoCrudTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private sealed record Forma(Guid Id, string Nome, bool Ativo);

    [Fact]
    public async Task CriarObterAtualizar()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var criar = await client.PostAsJsonAsync("/api/v1/formas-pagamento", new
        {
            nome = "Dinheiro",
            tipo = "Dinheiro",
            ehCartao = false,
            baixarAutomaticamente = true,
            ativo = true
        });
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        var criada = await criar.Content.ReadFromJsonAsync<Forma>();
        criada!.Nome.Should().Be("Dinheiro");

        var detalhe = await client.GetFromJsonAsync<Forma>($"/api/v1/formas-pagamento/{criada.Id}");
        detalhe!.Id.Should().Be(criada.Id);

        var atualizar = await client.PutAsJsonAsync($"/api/v1/formas-pagamento/{criada.Id}", new
        {
            nome = "Dinheiro (espécie)",
            tipo = "Dinheiro",
            ehCartao = false,
            baixarAutomaticamente = true,
            ativo = false
        });
        atualizar.StatusCode.Should().Be(HttpStatusCode.OK);
        var atualizada = await atualizar.Content.ReadFromJsonAsync<Forma>();
        atualizada!.Nome.Should().Be("Dinheiro (espécie)");
        atualizada.Ativo.Should().BeFalse();
    }

    [Fact]
    public async Task ObterPorId_Inexistente_DeveRetornar404()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var resp = await client.GetAsync($"/api/v1/formas-pagamento/{Guid.NewGuid()}");

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Atualizar_Inexistente_DeveRetornar404()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var resp = await client.PutAsJsonAsync($"/api/v1/formas-pagamento/{Guid.NewGuid()}", new
        {
            nome = "X",
            tipo = "Pix",
            ehCartao = false,
            baixarAutomaticamente = false,
            ativo = true
        });

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
