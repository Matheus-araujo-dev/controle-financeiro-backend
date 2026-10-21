namespace ControleFinanceiro.Infrastructure.Alertas;

public sealed class ResendOptions
{
    public const string SectionName = "Resend";

    public bool Enabled { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string FromEmail { get; set; } = "alertas@controle.app";
    public string FromName { get; set; } = "Controle Financeiro";
}
