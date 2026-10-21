using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ControleFinanceiro.Application.Common.Alertas;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ControleFinanceiro.Infrastructure.Alertas;

public sealed class ResendEmailService(
    HttpClient http,
    IOptions<ResendOptions> options,
    ILogger<ResendEmailService> logger) : IEmailAlertaService
{
    public async Task<bool> EnviarAsync(
        string destinatario,
        string assunto,
        string htmlBody,
        CancellationToken cancellationToken)
    {
        var opts = options.Value;
        if (!opts.Enabled || string.IsNullOrWhiteSpace(opts.ApiKey))
        {
            logger.LogDebug("Resend desativado. E-mail para {Dest} não enviado.", destinatario);
            return false;
        }

        var payload = new ResendEmailPayload(
            From: $"{opts.FromName} <{opts.FromEmail}>",
            To: [destinatario],
            Subject: assunto,
            Html: htmlBody);

        try
        {
            var response = await http.PostAsJsonAsync("emails", payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning("Resend retornou {Status}: {Body}", (int)response.StatusCode, body);
                return false;
            }

            logger.LogInformation("E-mail enviado via Resend → {Dest}: {Assunto}", destinatario, assunto);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao enviar e-mail via Resend para {Dest}.", destinatario);
            return false;
        }
    }

    private sealed record ResendEmailPayload(
        [property: JsonPropertyName("from")] string From,
        [property: JsonPropertyName("to")] string[] To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("html")] string Html);
}
