namespace ControleFinanceiro.Contracts.Errors;

public sealed record ApiErrorResponse(
    string Code,
    string Message,
    IReadOnlyDictionary<string, string[]> Errors,
    string TraceId);
