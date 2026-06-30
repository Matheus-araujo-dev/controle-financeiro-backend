using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ControleFinanceiro.Api.Configuration;

public sealed class DistributedCacheHealthCheck(IDistributedCache cache) : IHealthCheck
{
    private const string ProbeKey = "health_probe";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await cache.SetStringAsync(ProbeKey, "ok", new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
            }, cancellationToken);

            var value = await cache.GetStringAsync(ProbeKey, cancellationToken);
            return value == "ok"
                ? HealthCheckResult.Healthy("Distributed cache is available")
                : HealthCheckResult.Degraded("Distributed cache returned unexpected value");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Distributed cache is unavailable", ex);
        }
    }
}
