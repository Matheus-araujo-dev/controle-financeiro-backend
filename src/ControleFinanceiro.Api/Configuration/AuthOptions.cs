namespace ControleFinanceiro.Api.Configuration;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public const string DevelopmentMode = "Development";
    public const string JwtBearerMode = "JwtBearer";
    public const string SelfJwtMode = "SelfJwt";

    public string Mode { get; init; } = JwtBearerMode;
    public string DevelopmentUserHeader { get; init; } = "X-Debug-User";

    /// <summary>Família atribuída às requisições autenticadas em modo Development.</summary>
    public Guid DevelopmentFamiliaId { get; init; } = Guid.Parse("00000000-0000-0000-0000-000000000001");

    // Modo JwtBearer (authority externa, ex.: Auth0)
    public string Authority { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public bool RequireHttpsMetadata { get; init; }

    // Modo SelfJwt (login Google + JWT emitido pela própria API)
    public string GoogleClientId { get; init; } = string.Empty;
    public string JwtSigningKey { get; init; } = string.Empty;
    public string JwtIssuer { get; init; } = "controle-financeiro";
    public string JwtAudience { get; init; } = "controle-financeiro-api";
    public int AccessTokenMinutes { get; init; } = 30;
    public int RefreshTokenDays { get; init; } = 30;
}
