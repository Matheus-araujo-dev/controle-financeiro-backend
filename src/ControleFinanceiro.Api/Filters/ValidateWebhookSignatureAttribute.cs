using System.Security.Cryptography;
using System.Text;
using ControleFinanceiro.Api.Configuration;
using ControleFinanceiro.Contracts.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace ControleFinanceiro.Api.Filters;

/// <summary>
/// Valida a assinatura HMAC-SHA256 do corpo bruto da requisição.
/// Aceita os headers X-Webhook-Signature ou X-Hub-Signature-256 (formato Meta),
/// com valor "sha256=&lt;hex&gt;" ou apenas o hex.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ValidateWebhookSignatureAttribute : Attribute, IFilterFactory
{
    public bool IsReusable => false;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider) =>
        new ValidateWebhookSignatureFilter(
            serviceProvider.GetRequiredService<IOptions<WhatsappWebhookOptions>>(),
            serviceProvider.GetRequiredService<IHostEnvironment>(),
            serviceProvider.GetRequiredService<ILogger<ValidateWebhookSignatureFilter>>());
}

public sealed class ValidateWebhookSignatureFilter(
    IOptions<WhatsappWebhookOptions> options,
    IHostEnvironment environment,
    ILogger<ValidateWebhookSignatureFilter> logger) : IAsyncResourceFilter
{
    private static readonly string[] SignatureHeaders = ["X-Webhook-Signature", "X-Hub-Signature-256"];

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var secret = options.Value.WebhookSecret;

        if (string.IsNullOrWhiteSpace(secret))
        {
            if (environment.IsProduction())
            {
                logger.LogError("Webhook rejeitado: Whatsapp:WebhookSecret não está configurado em produção.");
                context.Result = Unauthorized(context, "Webhook não está habilitado.");
                return;
            }

            await next();
            return;
        }

        var request = context.HttpContext.Request;
        request.EnableBuffering();

        using var memoryStream = new MemoryStream();
        await request.Body.CopyToAsync(memoryStream, context.HttpContext.RequestAborted);
        request.Body.Position = 0;

        var providedSignature = ExtractSignature(request.Headers);
        if (providedSignature is null)
        {
            context.Result = Unauthorized(context, "Assinatura do webhook ausente.");
            return;
        }

        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), memoryStream.ToArray());

        if (!FixedTimeEquals(expected, providedSignature))
        {
            logger.LogWarning("Webhook rejeitado: assinatura HMAC inválida.");
            context.Result = Unauthorized(context, "Assinatura do webhook inválida.");
            return;
        }

        await next();
    }

    private static byte[]? ExtractSignature(IHeaderDictionary headers)
    {
        foreach (var headerName in SignatureHeaders)
        {
            var value = headers[headerName].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var hex = value.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)
                ? value["sha256=".Length..]
                : value;

            try
            {
                return Convert.FromHexString(hex);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        return null;
    }

    private static bool FixedTimeEquals(byte[] expected, byte[] provided) =>
        expected.Length == provided.Length && CryptographicOperations.FixedTimeEquals(expected, provided);

    private static UnauthorizedObjectResult Unauthorized(ResourceExecutingContext context, string message) =>
        new(new ApiErrorResponse(
            "WEBHOOK_UNAUTHORIZED",
            message,
            new Dictionary<string, string[]>(),
            context.HttpContext.TraceIdentifier));
}
