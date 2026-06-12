namespace ControleFinanceiro.Api.Configuration;

public sealed class WhatsappWebhookOptions
{
    public const string SectionName = "Whatsapp";

    /// <summary>
    /// Segredo usado para validar a assinatura HMAC-SHA256 do corpo do webhook.
    /// Vazio: o webhook só é aceito em ambientes de não-produção.
    /// </summary>
    public string WebhookSecret { get; init; } = string.Empty;
}
