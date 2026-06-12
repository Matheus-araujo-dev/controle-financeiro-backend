using ControleFinanceiro.Domain.Identidade;

namespace ControleFinanceiro.Application.Identidade;

public sealed record AccessTokenResult(string AccessToken, DateTime ExpiresAtUtc);

public interface ITokenService
{
    AccessTokenResult CreateAccessToken(Usuario usuario, Guid familiaId, PapelFamilia papel);

    string GenerateOpaqueToken();

    string HashToken(string token);

    TimeSpan RefreshTokenLifetime { get; }
}
