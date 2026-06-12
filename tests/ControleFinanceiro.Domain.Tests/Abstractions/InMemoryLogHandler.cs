using ControleFinanceiro.SharedKernel.Abstractions;

namespace ControleFinanceiro.Domain.Tests.Abstractions;

public class InMemoryLogHandler : ILogHandler
{
    private readonly List<LogEntry> _logs = new();

    public IReadOnlyList<LogEntry> Logs => _logs;

    public void LogInfo(string template, params object[] args)
    {
        _logs.Add(new LogEntry(LogLevel.Info, null, template, args));
    }

    public void LogWarning(string template, params object[] args)
    {
        _logs.Add(new LogEntry(LogLevel.Warning, null, template, args));
    }

    public void LogError(string template, params object[] args)
    {
        _logs.Add(new LogEntry(LogLevel.Error, null, template, args));
    }

    public void LogError(Exception exception, string template, params object[] args)
    {
        _logs.Add(new LogEntry(LogLevel.Error, exception, template, args));
    }

    public void LogInfo(string correlationId, string template, params object[] args)
    {
        _logs.Add(new LogEntry(LogLevel.Info, null, template, args) { CorrelationId = correlationId });
    }

    public void LogWarning(string correlationId, string template, params object[] args)
    {
        _logs.Add(new LogEntry(LogLevel.Warning, null, template, args) { CorrelationId = correlationId });
    }

    public void LogError(string correlationId, string template, params object[] args)
    {
        _logs.Add(new LogEntry(LogLevel.Error, null, template, args) { CorrelationId = correlationId });
    }

    public void LogError(string correlationId, Exception exception, string template, params object[] args)
    {
        _logs.Add(new LogEntry(LogLevel.Error, exception, template, args) { CorrelationId = correlationId });
    }

    public void Clear() => _logs.Clear();

    public bool HasMessage(string message) => _logs.Any(l => l.Message.Contains(message));

    public bool HasMessageContaining(string partialMessage) => _logs.Any(l => l.Message.Contains(partialMessage));

    public bool HasError() => _logs.Any(l => l.Level == LogLevel.Error);
}

public record LogEntry(
    LogLevel Level,
    Exception? Exception,
    string Template,
    object[] Args)
{
    public string? CorrelationId { get; init; }

    public string Message => string.Format(Template, Args);
}

public enum LogLevel
{
    Info,
    Warning,
    Error
}