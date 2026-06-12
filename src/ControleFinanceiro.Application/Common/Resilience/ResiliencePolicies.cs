using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace ControleFinanceiro.Application.Common.Resilience;

public interface IResiliencePolicy
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);
    Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);
}

public sealed class CircuitBreakerPolicy : IResiliencePolicy
{
    private readonly AsyncCircuitBreakerPolicy _circuitBreaker;

    public CircuitBreakerPolicy(string name, int exceptionsAllowedBeforeBreaking = 3, TimeSpan? durationOfBreak = null)
    {
        var duration = durationOfBreak ?? TimeSpan.FromSeconds(30);
        
        _circuitBreaker = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: exceptionsAllowedBeforeBreaking,
                durationOfBreak: duration,
                onBreak: (ex, timeSpan) =>
                {
                    Console.WriteLine($"[CircuitBreaker:{name}] Circuit opened for {timeSpan.TotalSeconds}s due to: {ex.Message}");
                },
                onReset: () =>
                {
                    Console.WriteLine($"[CircuitBreaker:{name}] Circuit reset.");
                },
                onHalfOpen: () =>
                {
                    Console.WriteLine($"[CircuitBreaker:{name}] Circuit half-open, testing...");
                });
    }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
    {
        return await _circuitBreaker.ExecuteAsync(action, cancellationToken);
    }

    public async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        await _circuitBreaker.ExecuteAsync(action, cancellationToken);
    }
}

public sealed class RetryPolicy : IResiliencePolicy
{
    private readonly AsyncRetryPolicy _retry;

    public RetryPolicy(string name, int retryCount = 3, TimeSpan? delay = null)
    {
        var retryDelay = delay ?? TimeSpan.FromSeconds(2);
        
        _retry = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: retryCount,
                sleepDurationProvider: retryAttempt => retryDelay * retryAttempt,
                onRetry: (ex, timeSpan, retryCount, context) =>
                {
                    Console.WriteLine($"[Retry:{name}] Attempt {retryCount} failed. Waiting {timeSpan.TotalSeconds}s. Error: {ex.Message}");
                });
    }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
    {
        return await _retry.ExecuteAsync(action, cancellationToken);
    }

    public async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        await _retry.ExecuteAsync(action, cancellationToken);
    }
}

public sealed class CompositePolicy : IResiliencePolicy
{
    private readonly IReadOnlyList<IResiliencePolicy> _policies;

    public CompositePolicy(params IResiliencePolicy[] policies)
    {
        _policies = policies.ToList();
    }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
    {
        foreach (var policy in _policies)
        {
            action = ct => policy.ExecuteAsync(action, ct);
        }
        
        return await action(cancellationToken);
    }

    public async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        foreach (var policy in _policies)
        {
            await policy.ExecuteAsync(action, cancellationToken);
        }
    }
}

public static class ResiliencePolicyFactory
{
    private static readonly Dictionary<string, IResiliencePolicy> _policies = new();

    public static IResiliencePolicy GetCircuitBreaker(string name, int exceptionsAllowedBeforeBreaking = 3, TimeSpan durationOfBreak = default)
    {
        if (durationOfBreak == default)
            durationOfBreak = TimeSpan.FromSeconds(30);
            
        if (!_policies.TryGetValue($"cb-{name}", out var policy))
        {
            policy = new CircuitBreakerPolicy(name, exceptionsAllowedBeforeBreaking, durationOfBreak);
            _policies[$"cb-{name}"] = policy;
        }
        
        return policy;
    }

    public static IResiliencePolicy GetRetry(string name, int retryCount = 3, TimeSpan delay = default)
    {
        if (delay == default)
            delay = TimeSpan.FromSeconds(2);
            
        if (!_policies.TryGetValue($"retry-{name}", out var policy))
        {
            policy = new RetryPolicy(name, retryCount, delay);
            _policies[$"retry-{name}"] = policy;
        }
        
        return policy;
    }

    public static IResiliencePolicy GetComposite(string name, int retryCount = 3, int exceptionsAllowedBeforeBreaking = 3)
    {
        var key = $"composite-{name}";
        
        if (!_policies.TryGetValue(key, out var policy))
        {
            var retry = GetRetry(name, retryCount);
            var circuitBreaker = GetCircuitBreaker(name, exceptionsAllowedBeforeBreaking);
            policy = new CompositePolicy(retry, circuitBreaker);
            _policies[key] = policy;
        }
        
        return policy;
    }
}