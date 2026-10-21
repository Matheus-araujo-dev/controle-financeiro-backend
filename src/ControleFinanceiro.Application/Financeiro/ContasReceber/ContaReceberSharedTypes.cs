namespace ControleFinanceiro.Application.Financeiro.ContasReceber;

internal sealed record ContaReceberRecorrenciaTemplate(
    string? NumeroDocumento,
    DateOnly DataEmissao,
    Guid? ResponsavelId,
    Guid PagadorId,
    DateOnly DataVencimento,
    Guid FormaPagamentoId,
    Guid? CartaoId,
    Guid? ContaBancariaId,
    decimal ValorOriginal,
    decimal ValorDesconto,
    decimal ValorJuros,
    decimal ValorMulta,
    string Descricao,
    string? Observacao,
    IReadOnlyCollection<RateioRecorrenciaTemplate> Rateios);

internal sealed record RateioRecorrenciaTemplate(Guid ContaGerencialId, decimal Valor);
