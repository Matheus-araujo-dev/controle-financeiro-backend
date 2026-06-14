using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Security;

public sealed class SecurityControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    [Fact]
    public async Task Me_WhenHeaderIsMissing_ShouldReturnUnauthorized()
    {
        using var client = _factory.CreateAnonymousClient();

        var response = await client.GetAsync("/api/v1/security/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_WhenDevelopmentUserHeaderIsPresent_ShouldReturnCurrentUser()
    {
        using var client = _factory.CreateAuthenticatedClient("codex-user");

        var response = await client.GetAsync("/api/v1/security/me");
        var payload = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        payload.Should().NotBeNull();
        payload!.IsAuthenticated.Should().BeTrue();
        Guid.TryParse(payload.UserId, out _).Should().BeTrue();
        payload.AuthMode.Should().Be("Development");
    }

    private sealed record CurrentUserResponse(bool IsAuthenticated, string? UserId, string AuthMode);
}
