namespace ControleFinanceiro.Application.FinanceAI;

public record WhatsappBridgeQrResponse(string Status, string? Qr);

public interface IWhatsappOutboundService
{
    Task EnviarAsync(string telefone, string texto, CancellationToken cancellationToken);
    Task EnviarMidiaAsync(string telefone, string tipo, string base64Content, string mimeType, string? nomeArquivo, string? legenda, CancellationToken cancellationToken);
    Task<WhatsappBridgeQrResponse> ObterQrAsync(CancellationToken cancellationToken);
}
