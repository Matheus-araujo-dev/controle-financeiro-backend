using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;

namespace ControleFinanceiro.Infrastructure.FinanceAI;

/// <summary>
/// Políticas de resiliência (retry com backoff exponencial + jitter e circuit breaker)
/// aplicadas aos HttpClients que falam com provedores externos (Anthropic, OpenAI, bridge WhatsApp).
/// Cobre erros transitórios HTTP (5xx, 408) e falhas de rede (HttpRequestException);
/// 429 é deliberadamente ignorado para não amplificar rate limit do provedor.
/// </summary>
internal static class HttpResiliencePolicies
{
    public static IAsyncPolicy<HttpResponseMessage> RetryPolicy(ILogger logger, string clientName)
    {
        var jitter = new Random();

        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt =>
                    TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 200)
                    + TimeSpan.FromMilliseconds(jitter.Next(0, 250)),
                onRetry: (outcome, delay, attempt, _) =>
                    logger.LogWarning(
                        "Tentativa {Attempt} para {Client} após falha transitória ({Status}); aguardando {Delay}ms.",
                        attempt,
                        clientName,
                        outcome.Result?.StatusCode,
                        delay.TotalMilliseconds));
    }

    public static IAsyncPolicy<HttpResponseMessage> CircuitBreakerPolicy(ILogger logger, string clientName)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (outcome, breakDelay) =>
                    logger.LogError(
                        "Circuit breaker ABERTO para {Client} por {Delay}s após falhas repetidas ({Status}).",
                        clientName,
                        breakDelay.TotalSeconds,
                        outcome.Result?.StatusCode),
                onReset: () => logger.LogInformation("Circuit breaker RESTAURADO para {Client}.", clientName));
    }
}
