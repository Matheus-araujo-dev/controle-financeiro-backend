using ControleFinanceiro.Contracts.Filters;
using ControleFinanceiro.Contracts.Financeiro.Common;
using ControleFinanceiro.Contracts.Financeiro.ContasPagar;

namespace ControleFinanceiro.Contracts.Financeiro.ContasReceber;

public sealed record ContaReceberListQueryRequest : ListQueryRequest
{
    public string? NumeroDocumento { get; init; }

    public string? Descricao { get; init; }

    public Guid? PagadorId { get; init; }

    public IReadOnlyCollection<Guid>? PagadorIds { get; init; }

    public Guid? FormaPagamentoId { get; init; }

    public IReadOnlyCollection<Guid>? FormaPagamentoIds { get; init; }

    public string? StatusCodigo { get; init; }

    public IReadOnlyCollection<string>? StatusCodigos { get; init; }

    public DateOnly? DataEmissaoInicial { get; init; }

    public DateOnly? DataEmissaoFinal { get; init; }

    public DateOnly? DataVencimentoInicial { get; init; }

    public DateOnly? DataVencimentoFinal { get; init; }

    public decimal? ValorMinimo { get; init; }

    public decimal? ValorMaximo { get; init; }

    public bool? EhRecorrente { get; init; }
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
    IReadOnlyCollection<RateioRequest> Rateios,
    RecorrenciaConfigRequest? Recorrencia);

public sealed record AtualizarContaReceberRequest(
    Guid Id,
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
    IReadOnlyCollection<RateioRequest> Rateios,
    RecorrenciaConfigRequest? Recorrencia);

public sealed record LiquidarContaReceberRequest(
    decimal ValorLiquidacao,
    DateOnly DataLiquidacao,
    Guid ContaBancariaId,
    Guid? FormaPagamentoId = null,
    bool AtualizarValorConta = false);

public sealed record ContaReceberResumoResponse(
    Guid Id,
    string? NumeroDocumento,
    string Descricao,
    Guid PagadorId,
    string PagadorNome,
    string? ResponsavelNome,
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
    Guid? GrupoParcelamentoId,
    bool EhRecorrente);

public sealed record ContaReceberListSummaryResponse(
    int TotalRegistros,
    decimal ValorTotal,
    decimal TotalPendente,
    decimal TotalVencido,
    decimal TotalVencendoHoje,
    decimal TotalLiquidado);

public sealed record ContaReceberListResponse(
    IReadOnlyCollection<ContaReceberResumoResponse> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    ContaReceberListSummaryResponse Summary);

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
    bool EhRecorrente,
    LancamentoOrigem Origem,
    RecorrenciaResponse? Recorrencia,
    IReadOnlyCollection<RateioResponse> Rateios,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
