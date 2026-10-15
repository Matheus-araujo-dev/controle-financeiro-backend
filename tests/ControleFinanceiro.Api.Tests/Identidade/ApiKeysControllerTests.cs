using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using ControleFinanceiro.Contracts.Auth;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Identidade;

public sealed class ApiKeysControllerTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    [Fact]
    public async Task Criar_DeveRetornarChaveComPrefixo()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var request = new CriarApiKeyRequest("Bot WhatsApp", null);
        var response = await client.PostAsJsonAsync("/api/v1/api-keys", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<ApiKeyCriadaResponse>();
        body.Should().NotBeNull();
        body!.Nome.Should().Be("Bot WhatsApp");
        body.Key.Should().StartWith("cfk_");
        body.KeyPrefix.Should().HaveLength(8);
        body.Key.Should().StartWith(body.KeyPrefix);
        body.Papel.Should().Be("Agente");
        body.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Criar_ComExpiracao_DeveRetornarData()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var expira = DateTime.UtcNow.AddDays(90);
        var request = new CriarApiKeyRequest("Bot Temp", expira);
        var response = await client.PostAsJsonAsync("/api/v1/api-keys", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ApiKeyCriadaResponse>();
        body!.ExpiraEmUtc.Should().BeCloseTo(expira, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Listar_DeveRetornarKeysCriadas()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/api-keys", new CriarApiKeyRequest("Key 1", null));
        await client.PostAsJsonAsync("/api/v1/api-keys", new CriarApiKeyRequest("Key 2", null));

        var response = await client.GetFromJsonAsync<List<ApiKeyResponse>>("/api/v1/api-keys");

        response.Should().HaveCount(2);
        response!.Select(k => k.Nome).Should().Contain(["Key 1", "Key 2"]);
        response.Should().AllSatisfy(k =>
        {
            k.Ativo.Should().BeTrue();
            k.Papel.Should().Be("Agente");
        });
    }

    [Fact]
    public async Task Revogar_DeveInativarKey()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/v1/api-keys", new CriarApiKeyRequest("Para Revogar", null));
        var created = await createResponse.Content.ReadFromJsonAsync<ApiKeyCriadaResponse>();

        var deleteResponse = await client.DeleteAsync($"/api/v1/api-keys/{created!.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var keys = await client.GetFromJsonAsync<List<ApiKeyResponse>>("/api/v1/api-keys");
        keys!.Should().ContainSingle().Which.Ativo.Should().BeFalse();
    }

    [Fact]
    public async Task Revogar_IdInexistente_DeveRetornar404()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var response = await client.DeleteAsync($"/api/v1/api-keys/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Endpoints_SemAutenticacao_DeveRetornar401()
    {
        using var client = _factory.CreateAnonymousClient();

        var getResponse = await client.GetAsync("/api/v1/api-keys");
        getResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var postResponse = await client.PostAsJsonAsync("/api/v1/api-keys", new CriarApiKeyRequest("Test", null));
        postResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Listar_SemKeys_DeveRetornarListaVazia()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var keys = await client.GetFromJsonAsync<List<ApiKeyResponse>>("/api/v1/api-keys");
        keys.Should().BeEmpty();
    }
}
