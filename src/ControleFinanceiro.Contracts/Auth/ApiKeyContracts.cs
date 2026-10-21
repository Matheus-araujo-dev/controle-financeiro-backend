using System.ComponentModel.DataAnnotations;

namespace ControleFinanceiro.Contracts.Auth;

public sealed record CriarApiKeyRequest(
    [Required] string Nome,
    DateTime? ExpiraEmUtc = null);

public sealed record ApiKeyResponse(
    Guid Id,
    string Nome,
    string KeyPrefix,
    Guid FamiliaId,
    string Papel,
    bool Ativo,
    DateTime? ExpiraEmUtc,
    DateTime? UltimoUsoEmUtc,
    DateTime CriadoEmUtc);

public sealed record ApiKeyCriadaResponse(
    Guid Id,
    string Nome,
    string KeyPrefix,
    string Key,
    Guid FamiliaId,
    string Papel,
    DateTime? ExpiraEmUtc,
    DateTime CriadoEmUtc);