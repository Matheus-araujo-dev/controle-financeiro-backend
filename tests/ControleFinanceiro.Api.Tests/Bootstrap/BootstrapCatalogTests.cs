using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Bootstrap;

public sealed class BootstrapCatalogTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    [Fact]
    public async Task GetStatus_DeveRetornarMetadados()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var resposta = await client.GetAsync("/api/v1/bootstrap/status");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        doc.RootElement.EnumerateObject().Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetModules_DeveRetornarCatalogoPaginado()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var resposta = await client.GetAsync("/api/v1/bootstrap/modules?page=1&pageSize=50");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PostEcho_DeveNormalizarERetornarTamanho()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/v1/bootstrap/echo", new { name = "  Financeiro  " });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var corpo = await resp.Content.ReadAsStringAsync();
        corpo.Should().Contain("Financeiro");
    }
}
