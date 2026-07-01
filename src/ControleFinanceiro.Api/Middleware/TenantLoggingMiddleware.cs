using System.Security.Claims;
using Serilog.Context;

namespace ControleFinanceiro.Api.Middleware;

/// <summary>
/// Enriches every Serilog log event produced within an HTTP request with UserId and TenantId
/// so that structured logs can be filtered/correlated by workspace without manual push calls in services.
/// </summary>
public sealed class TenantLoggingMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? context.User.FindFirstValue("sub");
            var workspaceId = context.User.FindFirstValue("workspaceId")
                ?? context.User.FindFirstValue("familiaId");

            using (LogContext.PushProperty("UserId", userId ?? string.Empty))
            using (LogContext.PushProperty("TenantId", workspaceId ?? string.Empty))
            {
                await next(context);
                return;
            }
        }

        await next(context);
    }
}
