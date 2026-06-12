namespace ControleFinanceiro.Application.Identidade;

public sealed record GoogleUserInfo(
    string Subject,
    string Email,
    string Nome,
    string? AvatarUrl);

public interface IGoogleTokenValidator
{
    Task<GoogleUserInfo> ValidateAsync(string idToken, CancellationToken cancellationToken);
}
