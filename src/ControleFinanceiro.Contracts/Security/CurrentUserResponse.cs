namespace ControleFinanceiro.Contracts.Security;

public sealed record CurrentUserResponse(
    bool IsAuthenticated,
    string? UserId,
    string AuthMode);
