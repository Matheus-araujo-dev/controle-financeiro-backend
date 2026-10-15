namespace ControleFinanceiro.Application.FinanceAI;

public record WhatsappBridgeQrResponse(string Status, string? Qr);

public interface IWhatsappOutboundService
{
    Task EnviarAsync(string telefone, string texto, CancellationToken cancellationToken);
    Task<WhatsappBridgeQrResponse> ObterQrAsync(CancellationToken cancellationToken);
}
