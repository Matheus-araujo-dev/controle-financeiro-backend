using System.Net;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Text.Json.Nodes;

namespace ControleFinanceiro.Api.Tests.Smoke;

public sealed class BootstrapEndpointsTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false
    });

    [Fact]
    public async Task GetHealth_ShouldReturnHealthy()
    {
        var response = await _client.GetAsync("/health");
        var payload = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, payload);
        payload.Should().Contain("Healthy");
    }

    [Fact]
    public async Task GetSwaggerUi_ShouldReturnDevelopmentSwaggerPage()
    {
        var response = await _client.GetAsync("/swagger/index.html");
        var payload = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, payload);
        payload.Should().Contain("Swagger UI");
    }

    [Fact]
    public async Task GetSwaggerDocument_ShouldExposeSupportedAuthenticationSchemes()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        var payload = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, payload);
        var document = JsonNode.Parse(payload);
        document.Should().NotBeNull();
        document!["components"]?["securitySchemes"]?["Bearer"]?["type"]?.GetValue<string>().Should().Be("http");
        document["components"]?["securitySchemes"]?["Bearer"]?["scheme"]?.GetValue<string>().Should().Be("bearer");
        document["components"]?["securitySchemes"]?["DebugUser"]?["type"]?.GetValue<string>().Should().Be("apiKey");
        document["components"]?["securitySchemes"]?["DebugUser"]?["name"]?.GetValue<string>().Should().Be("X-Debug-User");
        document["paths"]?["/api/v1/security/me"]?["get"]?["security"]?.AsArray().Count.Should().Be(2);
    }
}
