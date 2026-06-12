namespace ControleFinanceiro.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Auth";

    public string GoogleClientId { get; init; } = string.Empty;
    public string JwtSigningKey { get; init; } = string.Empty;
    public string JwtIssuer { get; init; } = "controle-financeiro";
    public string JwtAudience { get; init; } = "controle-financeiro-api";
    public int AccessTokenMinutes { get; init; } = 30;
    public int RefreshTokenDays { get; init; } = 30;
}
