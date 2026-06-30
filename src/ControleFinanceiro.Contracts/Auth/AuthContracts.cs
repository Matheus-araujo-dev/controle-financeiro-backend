using System.ComponentModel.DataAnnotations;

namespace ControleFinanceiro.Contracts.Auth;

public sealed record GoogleLoginRequest([Required] string IdToken);

public sealed record RefreshTokenRequest(string? RefreshToken);

public sealed record LogoutRequest(string? RefreshToken);

public sealed record FamiliaResumoResponse(
    Guid Id,
    string Nome,
    string Papel);

public sealed record UsuarioAutenticadoResponse(
    Guid Id,
    string Email,
    string Nome,
    string? AvatarUrl,
    FamiliaResumoResponse Familia);

public sealed record AuthTokenResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    string RefreshToken,
    UsuarioAutenticadoResponse Usuario);
