using System.Net;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ControleFinanceiro.Api.Tests.Smoke;

public sealed class CorsPolicyTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false
    });

    [Fact]
    public async Task GetPessoas_WithFrontendOrigin_ShouldReturnCorsHeaders()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/pessoas?page=1&pageSize=10&search=");
        request.Headers.Add("Origin", "http://127.0.0.1:5173");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins).Should().BeTrue();
        origins.Should().ContainSingle("http://127.0.0.1:5173");
    }
}
