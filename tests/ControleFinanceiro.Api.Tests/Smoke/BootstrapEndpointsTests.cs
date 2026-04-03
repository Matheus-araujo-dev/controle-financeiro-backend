using System.Net;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

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

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        payload.Should().Contain("Healthy");
    }

    [Fact]
    public async Task GetSwaggerUi_ShouldReturnDevelopmentSwaggerPage()
    {
        var response = await _client.GetAsync("/swagger/index.html");
        var payload = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        payload.Should().Contain("Swagger UI");
    }
}
