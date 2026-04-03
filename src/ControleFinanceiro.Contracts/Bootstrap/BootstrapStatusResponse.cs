namespace ControleFinanceiro.Contracts.Bootstrap;

public sealed record BootstrapStatusResponse(
    string ApplicationName,
    string ApiVersion,
    string AuthMode,
    string TraceId,
    DateTime GeneratedAtUtc);
