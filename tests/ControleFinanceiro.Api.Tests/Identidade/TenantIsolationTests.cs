using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using ControleFinanceiro.Application.Common.Exceptions;
using ControleFinanceiro.Application.Identidade;
using ControleFinanceiro.Contracts.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ControleFinanceiro.Api.Tests.Identidade;

public sealed class TenantIsolationTests : IDisposable
{
    private const string SigningKey = "chave-de-teste-para-jwt-com-tamanho-suficiente-123456";

    private readonly CustomWebApplicationFactory _factory = new();
    private readonly WebApplicationFactory<Program> _selfJwtFactory;

    public TenantIsolationTests()
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
    public async Task Pessoas_DeOutraFamilia_NaoDevemSerVisiveis()
    {
        using var client = _selfJwtFactory.CreateClient();

        var loginMaria = await LoginAsync(client, "token-maria");
        var loginPedro = await LoginAsync(client, "token-pedro");

        // Maria cria uma pessoa na família dela
        var createResponse = await SendAsync(
            client,
            HttpMethod.Post,
            "/api/v1/pessoas",
            new
            {
                nome = "Fornecedor da Maria",
                tipoPessoa = "Juridica",
                cpfCnpj = "12.345.678/0001-90"
            },
            loginMaria.AccessToken);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var pessoaId = (await ReadJsonAsync(createResponse)).GetProperty("id").GetGuid();

        // Maria vê a pessoa
        var listaMaria = await SendAsync(
            client, HttpMethod.Get, "/api/v1/pessoas", body: (object?)null, loginMaria.AccessToken);
        (await ReadJsonAsync(listaMaria)).GetProperty("items").GetArrayLength().Should().Be(1);

        // Pedro (outra família) não vê nada
        var listaPedro = await SendAsync(
            client, HttpMethod.Get, "/api/v1/pessoas", body: (object?)null, loginPedro.AccessToken);
        (await ReadJsonAsync(listaPedro)).GetProperty("items").GetArrayLength().Should().Be(0);

        // Acesso direto por id também retorna 404 para Pedro
        var detalhePedro = await SendAsync(
            client, HttpMethod.Get, $"/api/v1/pessoas/{pessoaId}", body: (object?)null, loginPedro.AccessToken);
        detalhePedro.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var detalheMaria = await SendAsync(
            client, HttpMethod.Get, $"/api/v1/pessoas/{pessoaId}", body: (object?)null, loginMaria.AccessToken);
        detalheMaria.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MembrosDaMesmaFamilia_DevemCompartilharDados()
    {
        using var client = _selfJwtFactory.CreateClient();

        var loginMaria = await LoginAsync(client, "token-maria");

        // Maria convida João para a família
        var conviteResponse = await SendAsync(
            client,
            HttpMethod.Post,
            "/api/v1/familias/convites",
            new { email = "joao@example.com", papel = "Membro" },
            loginMaria.AccessToken);
        var token = (await ReadJsonAsync(conviteResponse)).GetProperty("token").GetString();

        var loginJoao = await LoginAsync(client, "token-joao");
        await SendAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/familias/convites/{token}/aceitar",
            body: (object?)null,
            loginJoao.AccessToken);

        // Maria cria uma pessoa
        await SendAsync(
            client,
            HttpMethod.Post,
            "/api/v1/pessoas",
            new { nome = "Fornecedor Compartilhado", tipoPessoa = "Fisica" },
            loginMaria.AccessToken);

        // João (após novo login, com claim da família da Maria) vê a pessoa
        var loginJoaoAtualizado = await LoginAsync(client, "token-joao");
        var listaJoao = await SendAsync(
            client, HttpMethod.Get, "/api/v1/pessoas", body: (object?)null, loginJoaoAtualizado.AccessToken);

        (await ReadJsonAsync(listaJoao)).GetProperty("items").GetArrayLength().Should().Be(1);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    private static async Task<AuthTokenResponse> LoginAsync(HttpClient client, string idToken)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/google", new GoogleLoginRequest(idToken));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<AuthTokenResponse>())!;
    }

    private static async Task<HttpResponseMessage> SendAsync<TBody>(
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
                    new GoogleUserInfo("google-sub-joao", "joao@example.com", "João", null)),
                "token-pedro" => Task.FromResult(
                    new GoogleUserInfo("google-sub-pedro", "pedro@example.com", "Pedro", null)),
                _ => throw new AuthenticationFailedException("Token Google inválido.")
            };
        }
    }
}
