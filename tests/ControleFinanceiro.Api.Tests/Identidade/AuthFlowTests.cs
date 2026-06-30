using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using ControleFinanceiro.Application.Common.Exceptions;
using ControleFinanceiro.Application.Identidade;
using ControleFinanceiro.Contracts.Auth;
using ControleFinanceiro.Contracts.Familias;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ControleFinanceiro.Api.Tests.Identidade;

public sealed class AuthFlowTests : IDisposable
{
    private const string SigningKey = "chave-de-teste-para-jwt-com-tamanho-suficiente-123456";

    private readonly CustomWebApplicationFactory _factory = new();
    private readonly WebApplicationFactory<Program> _selfJwtFactory;

    public AuthFlowTests()
    {
        _selfJwtFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Auth:Mode", "SelfJwt");
            builder.UseSetting("Auth:JwtSigningKey", SigningKey);
            builder.UseSetting("Auth:GoogleClientId", "test-client-id");
            builder.ConfigureServices(services =>
            {
                services.AddScoped<IGoogleTokenValidator, FakeGoogleTokenValidator>();
            });
        });
    }

    public void Dispose()
    {
        _selfJwtFactory.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task LoginGoogle_PrimeiroLogin_DeveCriarUsuarioFamiliaETokens()
    {
        using var client = _selfJwtFactory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/google",
            new GoogleLoginRequest("token-maria"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<AuthTokenResponse>();
        payload.Should().NotBeNull();
        payload!.AccessToken.Should().NotBeNullOrWhiteSpace();
        // Refresh token is no longer returned in the body — it's set as an HttpOnly cookie.
        payload.RefreshToken.Should().BeNullOrEmpty();
        GetRefreshTokenFromSetCookie(response).Should().NotBeNullOrWhiteSpace();
        payload.Usuario.Email.Should().Be("maria@example.com");
        payload.Usuario.Familia.Nome.Should().Be("Família de Maria");
        payload.Usuario.Familia.Papel.Should().Be("Administrador");
    }

    [Fact]
    public async Task LoginGoogle_TokenInvalido_DeveRetornar401()
    {
        using var client = _selfJwtFactory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/google",
            new GoogleLoginRequest("token-invalido"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LoginGoogle_SegundoLogin_DeveReutilizarUsuarioEFamilia()
    {
        using var client = _selfJwtFactory.CreateClient();

        var primeiro = await LoginAsync(client, "token-maria");
        var segundo = await LoginAsync(client, "token-maria");

        segundo.Usuario.Id.Should().Be(primeiro.Usuario.Id);
        segundo.Usuario.Familia.Id.Should().Be(primeiro.Usuario.Familia.Id);
    }

    [Fact]
    public async Task FamiliaMinha_ComBearer_DeveRetornarMembros()
    {
        using var client = _selfJwtFactory.CreateClient();
        var login = await LoginAsync(client, "token-maria");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/familias/minha");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var familia = await response.Content.ReadFromJsonAsync<FamiliaDetalheResponse>();
        familia!.Membros.Should().ContainSingle(m => m.Email == "maria@example.com");
        familia.MeuPapel.Should().Be("Administrador");
    }

    [Fact]
    public async Task FamiliaMinha_SemToken_DeveRetornar401()
    {
        using var client = _selfJwtFactory.CreateClient();

        var response = await client.GetAsync("/api/v1/familias/minha");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Convite_FluxoCompleto_DeveAdicionarSegundoUsuarioNaFamilia()
    {
        using var client = _selfJwtFactory.CreateClient();
        var loginMaria = await LoginAsync(client, "token-maria");

        // Maria (admin) cria convite
        var conviteResponse = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/v1/familias/convites",
            new CriarConviteFamiliaRequest("joao@example.com", "Membro"),
            loginMaria.AccessToken);

        conviteResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var convite = await conviteResponse.Content.ReadFromJsonAsync<ConviteCriadoResponse>();
        convite!.Token.Should().NotBeNullOrWhiteSpace();

        // João loga e aceita o convite
        var loginJoao = await LoginAsync(client, "token-joao");
        var aceitarResponse = await SendJsonAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/familias/convites/{convite.Token}/aceitar",
            body: (object?)null,
            loginJoao.AccessToken);

        aceitarResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Novo login de João reflete a família de Maria
        var loginJoaoAtualizado = await LoginAsync(client, "token-joao");
        loginJoaoAtualizado.Usuario.Familia.Id.Should().Be(loginMaria.Usuario.Familia.Id);
        loginJoaoAtualizado.Usuario.Familia.Papel.Should().Be("Membro");

        // Família agora tem dois membros
        using var familiaRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/familias/minha");
        familiaRequest.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", loginJoaoAtualizado.AccessToken);
        var familiaResponse = await client.SendAsync(familiaRequest);
        var familia = await familiaResponse.Content.ReadFromJsonAsync<FamiliaDetalheResponse>();
        familia!.Membros.Should().HaveCount(2);
    }

    [Fact]
    public async Task Refresh_DeveRotacionarTokenEInvalidarAnterior()
    {
        using var client = _selfJwtFactory.CreateClient();

        // Login — capture old token from Set-Cookie header
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/google", new GoogleLoginRequest("token-maria"));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var oldToken = GetRefreshTokenFromSetCookie(loginResponse);
        oldToken.Should().NotBeNullOrWhiteSpace();

        // Refresh — old cookie sent automatically from the client's CookieContainer
        var refreshResponse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequest(null));
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify the cookie was rotated (new Set-Cookie issued)
        var newToken = GetRefreshTokenFromSetCookie(refreshResponse);
        newToken.Should().NotBeNullOrWhiteSpace();
        newToken.Should().NotBe(oldToken);

        // Reuso do token antigo deve falhar — fresh client has no cookie, sends old value via body
        using var freshClient = _selfJwtFactory.CreateClient();
        var reuso = await freshClient.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequest(oldToken));
        reuso.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_DeveRevogarRefreshToken()
    {
        using var client = _selfJwtFactory.CreateClient();
        var login = await LoginAsync(client, "token-maria");

        // Cookie is sent automatically from the container; body token is ignored since cookie takes priority.
        var logoutResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/logout",
            new LogoutRequest(login.RefreshToken));

        logoutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // After logout the cookie is cleared — subsequent refresh must fail.
        var refreshAposLogout = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshTokenRequest(null));

        refreshAposLogout.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Extracts the refreshToken cookie value from the Set-Cookie response header.</summary>
    private static string? GetRefreshTokenFromSetCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies)) return null;
        foreach (var cookie in cookies)
        {
            if (!cookie.StartsWith("refreshToken=", StringComparison.OrdinalIgnoreCase)) continue;
            var nameValue = cookie.Split(';')[0];
            var eq = nameValue.IndexOf('=');
            return eq >= 0 ? nameValue[(eq + 1)..] : null;
        }
        return null;
    }

    private static async Task<AuthTokenResponse> LoginAsync(HttpClient client, string idToken)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/google", new GoogleLoginRequest(idToken));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<AuthTokenResponse>())!;
    }

    private static async Task<HttpResponseMessage> SendJsonAsync<TBody>(
        HttpClient client,
        HttpMethod method,
        string url,
        TBody? body,
        string accessToken)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request);
    }

    private sealed record IdResponse(Guid Id);

    private sealed class FakeGoogleTokenValidator : IGoogleTokenValidator
    {
        public Task<GoogleUserInfo> ValidateAsync(string idToken, CancellationToken cancellationToken)
        {
            return idToken switch
            {
                "token-maria" => Task.FromResult(
                    new GoogleUserInfo("google-sub-maria", "maria@example.com", "Maria", null)),
                "token-joao" => Task.FromResult(
                    new GoogleUserInfo("google-sub-joao", "joao@example.com", "João", null)),
                _ => throw new AuthenticationFailedException("Token Google inválido.")
            };
        }
    }
}
