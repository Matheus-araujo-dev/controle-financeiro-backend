namespace ControleFinanceiro.Infrastructure.Alertas;

public sealed class VapidOptions
{
    public const string SectionName = "Vapid";

    public bool Enabled { get; set; }

    /// <summary>mailto:email ou https://seu-dominio.com</summary>
    public string Subject { get; set; } = string.Empty;

    public string PublicKey { get; set; } = string.Empty;

    public string PrivateKey { get; set; } = string.Empty;
}
