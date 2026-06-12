namespace ControleFinanceiro.SharedKernel.Abstractions;

public interface ILogHandler
{
    void LogInfo(string template, params object[] args);
    void LogWarning(string template, params object[] args);
    void LogError(string template, params object[] args);
    void LogError(Exception exception, string template, params object[] args);

    void LogInfo(string correlationId, string template, params object[] args);
    void LogWarning(string correlationId, string template, params object[] args);
    void LogError(string correlationId, string template, params object[] args);
    void LogError(string correlationId, Exception exception, string template, params object[] args);
}