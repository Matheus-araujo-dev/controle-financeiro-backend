namespace ControleFinanceiro.Api.Configuration;

public sealed class CorsOptions
{
    public const string SectionName = "Cors";
    public const string PolicyName = "FrontendClient";

    // Defaults para desenvolvimento local; em produção, sobrescrever via Cors__AllowedOrigins__0
    public string[] AllowedOrigins { get; init; } =
    [
        "http://localhost:5173",
        "http://127.0.0.1:5173",
        "http://localhost:5174",
        "http://127.0.0.1:5174",
        "http://localhost:5175",
        "http://127.0.0.1:5175"
    ];
}

// Em produção definir via variável de ambiente:
// Cors__AllowedOrigins__0=https://app.seudominio.com.br
