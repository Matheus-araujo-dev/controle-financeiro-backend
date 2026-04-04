namespace ControleFinanceiro.Contracts.Financeiro.Common;

public sealed record RateioRequest(Guid ContaGerencialId, decimal Valor);

public sealed record RateioResponse(
    Guid Id,
    Guid ContaGerencialId,
    string? ContaGerencialCodigo,
    string ContaGerencialDescricao,
    decimal Valor,
    decimal? Percentual);
