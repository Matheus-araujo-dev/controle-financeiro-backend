using System.Net;
using System.Security.Cryptography;
using System.Text;
using ControleFinanceiro.Api.Tests.Infrastructure;
using ControleFinanceiro.Contracts.Auth;
using ControleFinanceiro.Domain.Identidade;
using ControleFinanceiro.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ControleFinanceiro.Api.Tests.Identidade;

public sealed class ApiKeyAuthTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private static string HashKey(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hash);
    }

    private static string GenerateRawKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        return "cfk_" + Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private async Task<string> SeedApiKeyAsync(Guid familiaId, bool revogada = false, DateTime? expiraEmUtc = null)
    {
        var rawKey = GenerateRawKey();
        var keyHash = HashKey(rawKey);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.DefinirWorkspaceCorrente(familiaId);
        var apiKey = ApiKey.Criar("Test Bot", keyHash, rawKey[..8], familiaId, expiraEmUtc: expiraEmUtc);
        if (revogada) apiKey.Revogar();
        db.ApiKeys.Add(apiKey);
        await db.SaveChangesAsync();

        return rawKey;
    }

    [Fact]
    public async Task Request_ComApiKeyValida_DeveAutenticar()
    {
        await _factory.ResetDatabaseAsync();
        var familiaId = Guid.NewGuid();
        var rawKey = await SeedApiKeyAsync(familiaId);

        using var client = _factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", rawKey);

        var response = await client.GetAsync("/api/v1/api-keys");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Request_ComApiKeyInvalida_DeveRetornar401()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "cfk_esta-chave-nao-existe-no-banco-1234567890");

        var response = await client.GetAsync("/api/v1/api-keys");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Request_ComApiKeyRevogada_DeveRetornar401()
    {
        await _factory.ResetDatabaseAsync();
        var familiaId = Guid.NewGuid();
        var rawKey = await SeedApiKeyAsync(familiaId, revogada: true);

        using var client = _factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", rawKey);

        var response = await client.GetAsync("/api/v1/api-keys");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Request_ComApiKeyExpirada_DeveRetornar401()
    {
        await _factory.ResetDatabaseAsync();
        var familiaId = Guid.NewGuid();
        var rawKey = await SeedApiKeyAsync(familiaId, expiraEmUtc: DateTime.UtcNow.AddDays(-1));

        using var client = _factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", rawKey);

        var response = await client.GetAsync("/api/v1/api-keys");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Request_ComApiKeyCurta_DeveRetornar401()
    {
        using var client = _factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "short");

        var response = await client.GetAsync("/api/v1/api-keys");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public void Hash_DeveSerDeterministico()
    {
        var key = "cfk_test-deterministic";
        HashKey(key).Should().Be(HashKey(key));
        HashKey(key).Should().HaveLength(64);
    }
}
