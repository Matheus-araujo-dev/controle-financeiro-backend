namespace ControleFinanceiro.Contracts.Bootstrap;

public sealed record BootstrapStatusResponse(
    string ApplicationName,
    string ApiVersion,
    string TraceId,
    DateTime GeneratedAtUtc);
