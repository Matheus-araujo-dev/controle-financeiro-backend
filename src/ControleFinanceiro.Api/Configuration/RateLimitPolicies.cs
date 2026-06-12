using System.Threading.RateLimiting;

namespace ControleFinanceiro.Api.Configuration;

public static class RateLimitPolicies
{
    public const string StrictPolicy = "Strict";
    public const string StandardPolicy = "Standard";
    public const string RelaxedPolicy = "Relaxed";

    public static readonly Dictionary<string, FixedWindowRateLimiterOptions> Policies = new()
    {
        [StrictPolicy] = new()
        {
            AutoReplenishment = true,
            PermitLimit = 5,
            QueueLimit = 0,
            Window = TimeSpan.FromMinutes(1)
        },
        [StandardPolicy] = new()
        {
            AutoReplenishment = true,
            PermitLimit = 100,
            QueueLimit = 0,
            Window = TimeSpan.FromMinutes(1)
        },
        [RelaxedPolicy] = new()
        {
            AutoReplenishment = true,
            PermitLimit = 500,
            QueueLimit = 0,
            Window = TimeSpan.FromMinutes(1)
        }
    };

    public const string DefaultPolicy = StandardPolicy;
}