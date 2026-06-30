using ControleFinanceiro.Api.Configuration;

namespace ControleFinanceiro.Api.Tests.Configuration;

public class RateLimitPoliciesTests
{
    [Fact]
    public void PoliciesDictionary_ContemTodasAsPoliticas()
    {
        Assert.Contains(RateLimitPolicies.StrictPolicy, RateLimitPolicies.Policies);
        Assert.Contains(RateLimitPolicies.StandardPolicy, RateLimitPolicies.Policies);
        Assert.Contains(RateLimitPolicies.RelaxedPolicy, RateLimitPolicies.Policies);
        Assert.Contains(RateLimitPolicies.AiPolicy, RateLimitPolicies.Policies);
    }

    [Fact]
    public void Politicas_LimitesCorretos()
    {
        Assert.Equal(5, RateLimitPolicies.Policies[RateLimitPolicies.StrictPolicy].PermitLimit);
        Assert.Equal(100, RateLimitPolicies.Policies[RateLimitPolicies.StandardPolicy].PermitLimit);
        Assert.Equal(500, RateLimitPolicies.Policies[RateLimitPolicies.RelaxedPolicy].PermitLimit);
        Assert.Equal(30, RateLimitPolicies.Policies[RateLimitPolicies.AiPolicy].PermitLimit);
    }

    [Fact]
    public void DefaultPolicy_EhStandard()
    {
        Assert.Equal(RateLimitPolicies.StandardPolicy, RateLimitPolicies.DefaultPolicy);
    }
}
