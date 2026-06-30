using ControleFinanceiro.Domain.Cadastros.Cartoes;

namespace ControleFinanceiro.Application.Financeiro.ContasPagar;

internal sealed record ContaPagarValidationContext(bool LiquidarNaCriacao, bool CompraCartao, Cartao? Cartao);

internal sealed record ContaPagarRecorrenciaTemplate(
    string? NumeroDocumento,
    DateOnly DataEmissao,
    Guid? ResponsavelCompraId,
    Guid RecebedorId,
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
