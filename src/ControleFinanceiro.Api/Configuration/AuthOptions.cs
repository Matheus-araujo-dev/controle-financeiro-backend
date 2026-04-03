namespace ControleFinanceiro.Api.Configuration;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public string Mode { get; init; } = "Development";
    public string DevelopmentUserHeader { get; init; } = "X-Debug-User";
    public string Authority { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public bool RequireHttpsMetadata { get; init; }
}
