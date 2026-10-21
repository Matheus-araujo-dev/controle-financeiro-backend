using System.Net.Http.Json;
using ControleFinanceiro.Application.FinanceAI;
using Microsoft.Extensions.Logging;

namespace ControleFinanceiro.Infrastructure.FinanceAI;

public sealed class WhatsappOutboundService(
    HttpClient httpClient,
    ILogger<WhatsappOutboundService> logger) : IWhatsappOutboundService
{
    public async Task EnviarAsync(string telefone, string texto, CancellationToken cancellationToken)
    {
        try
        {
            var payload = new { telefone, texto };
            var response = await httpClient.PostAsJsonAsync("/enviar", payload, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning("Bridge outbound retornou {Status} para {Telefone}: {Body}",
                    (int)response.StatusCode, telefone, body);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao enviar mensagem proativa para {Telefone}.", telefone);
        }
    }

    public async Task EnviarMidiaAsync(string telefone, string tipo, string base64Content, string mimeType, string? nomeArquivo, string? legenda, CancellationToken cancellationToken)
    {
        try
        {
            var payload = new { telefone, tipo, base64 = base64Content, mimeType, nomeArquivo, legenda };
            var response = await httpClient.PostAsJsonAsync("/enviar-midia", payload, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning("Bridge outbound mídia retornou {Status} para {Telefone}: {Body}",
                    (int)response.StatusCode, telefone, body);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao enviar mídia para {Telefone}.", telefone);
        }
    }

    public async Task<WhatsappBridgeQrResponse> ObterQrAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.GetAsync("/qr", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Bridge /qr retornou {Status}.", (int)response.StatusCode);
                return new WhatsappBridgeQrResponse("error", null);
            }

            var result = await response.Content.ReadFromJsonAsync<BridgeQrDto>(cancellationToken);
            return new WhatsappBridgeQrResponse(result?.Status ?? "unknown", result?.Qr);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao obter QR do bridge.");
            return new WhatsappBridgeQrResponse("error", null);
        }
    }

    private sealed record BridgeQrDto(string? Status, string? Qr);
}
