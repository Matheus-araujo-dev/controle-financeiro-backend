namespace ControleFinanceiro.Infrastructure.FinanceAI;

public sealed class WhatsappBridgeOptions
{
    public const string SectionName = "WhatsappBridge";

    public string ApiKey { get; set; } = string.Empty;

    public string HmacSecret { get; set; } = string.Empty;

    /// <summary>URL do servidor outbound do bridge Node.js (porta 3001 por padrão).</summary>
    public string OutboundUrl { get; set; } = "http://localhost:3001";
}
