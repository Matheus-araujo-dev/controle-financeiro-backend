using ControleFinanceiro.SharedKernel.Abstractions;
using System.Security.Cryptography;
using System.Text;
using ControleFinanceiro.Contracts.Auth;
using ControleFinanceiro.Domain.Identidade;
using ControleFinanceiro.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Identidade;

public sealed class ApiKeyAppService(
    IAppDbContext dbContext,
    ICurrentUser currentUser)
{
    public async Task<ApiKeyCriadaResponse> CriarAsync(CriarApiKeyRequest request, CancellationToken ct)
    {
        var familiaId = currentUser.FamiliaId
            ?? throw new InvalidOperationException("Workspace não identificado.");

        var rawKey = GenerateRawKey();
        var keyHash = HashKey(rawKey);
        var keyPrefix = rawKey[..8];

        var apiKey = ApiKey.Criar(
            request.Nome,
            keyHash,
            keyPrefix,
            familiaId,
            expiraEmUtc: request.ExpiraEmUtc);

        dbContext.ApiKeys.Add(apiKey);
        await dbContext.SaveChangesAsync(ct);

        return new ApiKeyCriadaResponse(
            apiKey.Id,
            apiKey.Nome,
            keyPrefix,
            rawKey,
            familiaId,
            apiKey.Papel,
            apiKey.ExpiraEmUtc,
            apiKey.CreatedAtUtc);
    }

    public async Task<IReadOnlyList<ApiKeyResponse>> ListarAsync(CancellationToken ct)
    {
        var familiaId = currentUser.FamiliaId
            ?? throw new InvalidOperationException("Workspace não identificado.");

        return await dbContext.ApiKeys
            .Where(k => k.FamiliaId == familiaId)
            .OrderByDescending(k => k.CreatedAtUtc)
            .Select(k => new ApiKeyResponse(
                k.Id,
                k.Nome,
                k.KeyPrefix,
                k.FamiliaId,
                k.Papel,
                k.Ativo,
                k.ExpiraEmUtc,
                k.UltimoUsoEmUtc,
                k.CreatedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<bool> RevogarAsync(Guid id, CancellationToken ct)
    {
        var familiaId = currentUser.FamiliaId
            ?? throw new InvalidOperationException("Workspace não identificado.");

        var apiKey = await dbContext.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == id && k.FamiliaId == familiaId, ct);

        if (apiKey is null)
            return false;

        apiKey.Revogar();
        await dbContext.SaveChangesAsync(ct);
        return true;
    }

    private static string GenerateRawKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        return "cfk_" + Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    internal static string HashKey(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hash);
    }
}