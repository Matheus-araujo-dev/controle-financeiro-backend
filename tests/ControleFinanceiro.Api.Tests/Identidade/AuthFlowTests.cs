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
        payload.RefreshToken.Should().BeNullOrEmpty();
        GetRefreshTokenFromSetCookie(response).Should().NotBeNullOrWhiteSpace();
        payload.Usuario.Email.Should().Be("maria@example.com");
        payload.Usuario.Workspace.Nome.Should().Be("Espaco de Maria");
        payload.Usuario.Familia.Nome.Should().Be("Espaco de Maria");
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
    public async Task ObterConvite_Publico_DevePermitirAcessoSemLogin()
    {
        using var client = _selfJwtFactory.CreateClient();
        var loginMaria = await LoginAsync(client, "token-maria");
        var convite = await CriarConviteAsync(client, loginMaria.AccessToken, "joao@example.com");

        var response = await client.GetAsync($"/api/v1/familias/convites/{convite.Token}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ConviteDetalhePublicoResponse>();
        payload.Should().NotBeNull();
        payload!.EmailConvidado.Should().Be("joao@example.com");
        payload.Valido.Should().BeTrue();
    }

    [Fact]
    public async Task CriarWorkspace_DentroDoLimite_DeveCriarESetarSessaoNova()
    {
        using var client = _selfJwtFactory.CreateClient();
        var maria = await LoginAsync(client, "token-maria");

        var response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/v1/familias",
            new CriarWorkspaceRequest(null),
            maria.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var payload = await response.Content.ReadFromJsonAsync<SelecionarFamiliaResponse>();
        payload.Should().NotBeNull();
        payload!.Sessao.Usuario.Workspace.Id.Should().NotBe(maria.Usuario.Workspace.Id);
        payload.Sessao.Usuario.Workspace.Papel.Should().Be("Administrador");
        GetRefreshTokenFromSetCookie(response).Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Convite_FluxoCompleto_DeveAdicionarSegundoUsuarioNaFamilia()
    {
        using var client = _selfJwtFactory.CreateClient();
        var loginMaria = await LoginAsync(client, "token-maria");

        var convite = await CriarConviteAsync(client, loginMaria.AccessToken, "joao@example.com");

        var loginJoao = await LoginAsync(client, "token-joao");
        var aceitarResponse = await SendJsonAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/familias/convites/{convite.Token}/aceitar",
            body: (object?)null,
            loginJoao.AccessToken);

        aceitarResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginJoaoAtualizado = await LoginAsync(client, "token-joao");
        loginJoaoAtualizado.Usuario.Familia.Id.Should().Be(loginMaria.Usuario.Familia.Id);
        loginJoaoAtualizado.Usuario.Familia.Papel.Should().Be("Membro");

        using var familiaRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/familias/minha");
        familiaRequest.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", loginJoaoAtualizado.AccessToken);
        var familiaResponse = await client.SendAsync(familiaRequest);
        var familia = await familiaResponse.Content.ReadFromJsonAsync<FamiliaDetalheResponse>();
        familia!.Membros.Should().HaveCount(2);
    }

    [Fact]
    public async Task AceitarConvite_QuartaParticipacao_DeveFalharCom400()
    {
        using var client = _selfJwtFactory.CreateClient();

        var maria = await LoginAsync(client, "token-maria");
        var ana = await LoginAsync(client, "token-ana");
        var carla = await LoginAsync(client, "token-carla");
        var joao = await LoginAsync(client, "token-joao");

        var conviteMaria = await CriarConviteAsync(client, maria.AccessToken, "joao@example.com");
        var conviteAna = await CriarConviteAsync(client, ana.AccessToken, "joao@example.com");
        var conviteCarla = await CriarConviteAsync(client, carla.AccessToken, "joao@example.com");

        (await AceitarConviteAsync(client, joao.AccessToken, conviteMaria.Token)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await AceitarConviteAsync(client, joao.AccessToken, conviteAna.Token)).StatusCode.Should().Be(HttpStatusCode.OK);

        var quarta = await AceitarConviteAsync(client, joao.AccessToken, conviteCarla.Token);

        quarta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var erro = await quarta.Content.ReadAsStringAsync();
        erro.Should().Contain("Limite máximo de 3 participações atingido");
    }

    [Fact]
    public async Task ListarFamiliasESelecionarAtiva_DeveEmitirNovaSessao()
    {
        using var client = _selfJwtFactory.CreateClient();

        var maria = await LoginAsync(client, "token-maria");
        var ana = await LoginAsync(client, "token-ana");
        var joao = await LoginAsync(client, "token-joao");

        var conviteMaria = await CriarConviteAsync(client, maria.AccessToken, "joao@example.com");
        var conviteAna = await CriarConviteAsync(client, ana.AccessToken, "joao@example.com");

        (await AceitarConviteAsync(client, joao.AccessToken, conviteMaria.Token)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await AceitarConviteAsync(client, joao.AccessToken, conviteAna.Token)).StatusCode.Should().Be(HttpStatusCode.OK);

        var joaoAtualizado = await LoginAsync(client, "token-joao");

        using var listarRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/familias");
        listarRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", joaoAtualizado.AccessToken);
        var listarResponse = await client.SendAsync(listarRequest);

        listarResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var familias = await listarResponse.Content.ReadFromJsonAsync<List<ParticipacaoFamiliaResponse>>();
        familias.Should().NotBeNull();
        familias!.Should().HaveCount(3);
        familias.Should().Contain(f => f.Id == joaoAtualizado.Usuario.Familia.Id && f.Ativa);

        var selecionarResponse = await SendJsonAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/familias/{maria.Usuario.Familia.Id}/selecionar",
            body: (object?)null,
            joaoAtualizado.AccessToken);

        selecionarResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var sessao = await selecionarResponse.Content.ReadFromJsonAsync<SelecionarFamiliaResponse>();
        sessao.Should().NotBeNull();
        sessao!.Sessao.Usuario.Familia.Id.Should().Be(maria.Usuario.Familia.Id);
        sessao.Sessao.RefreshToken.Should().BeNullOrEmpty();
        GetRefreshTokenFromSetCookie(selecionarResponse).Should().NotBeNullOrWhiteSpace();

        using var minhaFamiliaRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/familias/minha");
        minhaFamiliaRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessao.Sessao.AccessToken);
        var minhaFamiliaResponse = await client.SendAsync(minhaFamiliaRequest);

        minhaFamiliaResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var familiaAtiva = await minhaFamiliaResponse.Content.ReadFromJsonAsync<FamiliaDetalheResponse>();
        familiaAtiva!.Id.Should().Be(maria.Usuario.Familia.Id);
    }

    [Fact]
    public async Task Refresh_DeveRotacionarTokenEInvalidarAnterior()
    {
        using var client = _selfJwtFactory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/google", new GoogleLoginRequest("token-maria"));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var oldToken = GetRefreshTokenFromSetCookie(loginResponse);
        oldToken.Should().NotBeNullOrWhiteSpace();

        var refreshResponse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequest(null));
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var newToken = GetRefreshTokenFromSetCookie(refreshResponse);
        newToken.Should().NotBeNullOrWhiteSpace();
        newToken.Should().NotBe(oldToken);

        using var freshClient = _selfJwtFactory.CreateClient();
        var reuso = await freshClient.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequest(oldToken));
        reuso.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_DeveRevogarRefreshToken()
    {
        using var client = _selfJwtFactory.CreateClient();
        var login = await LoginAsync(client, "token-maria");

        var logoutResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/logout",
            new LogoutRequest(login.RefreshToken));

        logoutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var refreshAposLogout = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshTokenRequest(null));

        refreshAposLogout.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<ConviteCriadoResponse> CriarConviteAsync(HttpClient client, string accessToken, string email)
    {
        var response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/v1/familias/convites",
            new CriarConviteFamiliaRequest(email, "Membro"),
            accessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ConviteCriadoResponse>())!;
    }

    private static Task<HttpResponseMessage> AceitarConviteAsync(HttpClient client, string accessToken, string token) =>
        SendJsonAsync(client, HttpMethod.Post, $"/api/v1/familias/convites/{token}/aceitar", body: (object?)null, accessToken);

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

    private sealed class FakeGoogleTokenValidator : IGoogleTokenValidator
    {
        public Task<GoogleUserInfo> ValidateAsync(string idToken, CancellationToken cancellationToken)
        {
            return idToken switch
            {
                "token-maria" => Task.FromResult(
                    new GoogleUserInfo("google-sub-maria", "maria@example.com", "Maria", null)),
                "token-joao" => Task.FromResult(
                    new GoogleUserInfo("google-sub-joao", "joao@example.com", "Joao", null)),
                "token-ana" => Task.FromResult(
                    new GoogleUserInfo("google-sub-ana", "ana@example.com", "Ana", null)),
                "token-carla" => Task.FromResult(
                    new GoogleUserInfo("google-sub-carla", "carla@example.com", "Carla", null)),
                _ => throw new AuthenticationFailedException("Token Google inválido.")
            };
        }
    }
}


