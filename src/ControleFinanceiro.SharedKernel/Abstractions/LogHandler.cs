using Microsoft.Extensions.Logging;

namespace ControleFinanceiro.SharedKernel.Abstractions;

public class LogHandler<T> : ILogHandler
{
    private readonly ILogger<T> _logger;

    public LogHandler(ILogger<T> logger)
    {
        _logger = logger;
    }

    public void LogInfo(string template, params object[] args)
    {
        _logger.LogInformation(template, args);
    }

    public void LogWarning(string template, params object[] args)
    {
        _logger.LogWarning(template, args);
    }

    public void LogError(string template, params object[] args)
    {
        _logger.LogError(template, args);
    }

    public void LogError(Exception exception, string template, params object[] args)
    {
        _logger.LogError(exception, template, args);
    }

    public void LogInfo(string correlationId, string template, params object[] args)
    {
        using var scope = BeginCorrelationScope(correlationId);
        _logger.LogInformation(template, args);
    }

    public void LogWarning(string correlationId, string template, params object[] args)
    {
        using var scope = BeginCorrelationScope(correlationId);
        _logger.LogWarning(template, args);
    }

    public void LogError(string correlationId, string template, params object[] args)
    {
        using var scope = BeginCorrelationScope(correlationId);
        _logger.LogError(template, args);
    }

    public void LogError(string correlationId, Exception exception, string template, params object[] args)
    {
        using var scope = BeginCorrelationScope(correlationId);
        _logger.LogError(exception, template, args);
    }

    private static IDisposable? BeginCorrelationScope(string correlationId)
    {
        return new CorrelationScope(correlationId);
    }

    private class CorrelationScope : IDisposable
    {
        private readonly string _correlationId;

        public CorrelationScope(string correlationId)
        {
            _correlationId = correlationId;
        }

        public void Dispose()
        {
        }
    }
}