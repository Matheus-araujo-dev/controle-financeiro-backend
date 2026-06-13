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
}
