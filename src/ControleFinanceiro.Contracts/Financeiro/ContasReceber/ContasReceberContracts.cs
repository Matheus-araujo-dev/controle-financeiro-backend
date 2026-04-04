using ControleFinanceiro.Contracts.Filters;
using ControleFinanceiro.Contracts.Financeiro.Common;
using ControleFinanceiro.Contracts.Financeiro.ContasPagar;

namespace ControleFinanceiro.Contracts.Financeiro.ContasReceber;

public sealed record ContaReceberListQueryRequest : ListQueryRequest
{
    public Guid? PagadorId { get; init; }

    public Guid? FormaPagamentoId { get; init; }

    public string? StatusCodigo { get; init; }

    public DateOnly? DataVencimentoInicial { get; init; }

    public DateOnly? DataVencimentoFinal { get; init; }
}

public sealed record CriarContaReceberRequest(
    string? NumeroDocumento,
    DateOnly DataEmissao,
    Guid? ResponsavelId,
    Guid PagadorId,
    DateOnly DataVencimento,
    Guid FormaPagamentoId,
    Guid? CartaoId,
    Guid? ContaBancariaId,
    DateOnly? DataLiquidacao,
    decimal ValorOriginal,
    decimal ValorDesconto,
    decimal ValorJuros,
    decimal ValorMulta,
    int QuantidadeParcelas,
    string Descricao,
    string? Observacao,
    IReadOnlyCollection<RateioRequest> Rateios);

public sealed record AtualizarContaReceberRequest(
    string? NumeroDocumento,
    DateOnly DataEmissao,
    Guid? ResponsavelId,
    Guid PagadorId,
    DateOnly DataVencimento,
    Guid FormaPagamentoId,
    Guid? CartaoId,
    Guid? ContaBancariaId,
    DateOnly? DataLiquidacao,
    decimal ValorOriginal,
    decimal ValorDesconto,
    decimal ValorJuros,
    decimal ValorMulta,
    int QuantidadeParcelas,
    string Descricao,
    string? Observacao,
    IReadOnlyCollection<RateioRequest> Rateios);

public sealed record LiquidarContaReceberRequest(DateOnly DataLiquidacao, Guid ContaBancariaId);

public sealed record ContaReceberResumoResponse(
    Guid Id,
    string? NumeroDocumento,
    string Descricao,
    Guid PagadorId,
    string PagadorNome,
    DateOnly DataEmissao,
    DateOnly DataVencimento,
    DateOnly? DataLiquidacao,
    Guid FormaPagamentoId,
    string FormaPagamentoNome,
    decimal ValorLiquido,
    string StatusCodigo,
    string StatusNome,
    int QuantidadeParcelas,
    int NumeroParcela,
    Guid? GrupoParcelamentoId);

public sealed record ContaReceberDetalheResponse(
    Guid Id,
    string? NumeroDocumento,
    DateOnly DataEmissao,
    Guid? ResponsavelId,
    string? ResponsavelNome,
    Guid PagadorId,
    string PagadorNome,
    DateOnly DataVencimento,
    DateOnly? DataLiquidacao,
    Guid FormaPagamentoId,
    string FormaPagamentoNome,
    bool FormaPagamentoEhCartao,
    bool FormaPagamentoBaixarAutomaticamente,
    Guid? CartaoId,
    string? CartaoNome,
    Guid? ContaBancariaId,
    string? ContaBancariaNome,
    decimal ValorOriginal,
    decimal ValorDesconto,
    decimal ValorJuros,
    decimal ValorMulta,
    decimal ValorLiquido,
    int QuantidadeParcelas,
    int NumeroParcela,
    Guid? GrupoParcelamentoId,
    string Descricao,
    string? Observacao,
    string StatusCodigo,
    string StatusNome,
    LancamentoOrigem Origem,
    IReadOnlyCollection<RateioResponse> Rateios,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
