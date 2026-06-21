using System.Security.Cryptography;
using System.Text;
using ControleFinanceiro.Infrastructure.FinanceAI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace ControleFinanceiro.Api.Filters;

/// <summary>
/// Valida X-Internal-ApiKey e X-Signature (HMAC-SHA256 do corpo) enviados pela bridge Node.js.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class InternalApiKeyAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var options = context.HttpContext.RequestServices
            .GetRequiredService<IOptions<WhatsappBridgeOptions>>().Value;

        if (string.IsNullOrEmpty(options.ApiKey))
        {
            context.Result = new StatusCodeResult(503);
            return;
        }

        var apiKey = context.HttpContext.Request.Headers["X-Internal-ApiKey"].FirstOrDefault();
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(apiKey ?? string.Empty),
                Encoding.UTF8.GetBytes(options.ApiKey)))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (!string.IsNullOrEmpty(options.HmacSecret))
        {
            var signature = context.HttpContext.Request.Headers["X-Signature"].FirstOrDefault();
            context.HttpContext.Request.EnableBuffering();
            var body = await new StreamReader(context.HttpContext.Request.Body, Encoding.UTF8, leaveOpen: true).ReadToEndAsync();
            context.HttpContext.Request.Body.Position = 0;

            var expected = ComputeHmac(body, options.HmacSecret);
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(signature ?? string.Empty),
                    Encoding.UTF8.GetBytes(expected)))
            {
                context.Result = new UnauthorizedResult();
                return;
            }
        }

        await next();
    }

    private static string ComputeHmac(string body, string secret)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var data = Encoding.UTF8.GetBytes(body);
        var hash = HMACSHA256.HashData(key, data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
