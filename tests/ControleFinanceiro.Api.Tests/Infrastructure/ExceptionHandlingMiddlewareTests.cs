using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Infrastructure;

/// <summary>
/// Testa as respostas do ExceptionHandlingMiddleware via endpoints HTTP reais.
/// </summary>
public sealed class ExceptionHandlingMiddlewareTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    [Fact]
    public async Task Middleware_ApplicationValidationException_Retorna400()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        // POST com nome vazio dispara ApplicationValidationException (validação de nome duplicado/vazio)
        var response = await client.PostAsJsonAsync("/api/v1/formas-pagamento", new
        {
            nome = "",
            tipo = "Pix",
            ehCartao = false,
            baixarAutomaticamente = false,
            ativo = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Middleware_EndpointInexistente_Retorna404()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/rota-que-nao-existe");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Middleware_RecursoNaoEncontrado_Retorna404()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/formas-pagamento/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
