using ControleFinanceiro.Contracts.Filters;

namespace ControleFinanceiro.Contracts.Financeiro.Faturas;

public sealed record FaturaListQueryRequest : ListQueryRequest
{
    public Guid? CartaoId { get; init; }

    public IReadOnlyCollection<Guid>? CartaoIds { get; init; }

    public string? Competencia { get; init; }

    public string? StatusCodigo { get; init; }

    public IReadOnlyCollection<string>? StatusCodigos { get; init; }

    public DateOnly? DataVencimentoInicial { get; init; }

    public DateOnly? DataVencimentoFinal { get; init; }

    public DateOnly? DataFechamentoInicial { get; init; }

    public DateOnly? DataFechamentoFinal { get; init; }
}

public sealed record PagarFaturaRequest(
    DateOnly DataPagamento,
    Guid ContaBancariaPagamentoId,
    string? Observacao);

public sealed record FaturaResumoResponse(
    Guid Id,
    Guid CartaoId,
    string CartaoNome,
    string Competencia,
    DateOnly DataFechamento,
    DateOnly DataVencimento,
    decimal ValorTotal,
    DateOnly? DataPagamento,
    string StatusCodigo,
    string StatusNome,
    int QuantidadeItens);

public sealed record FaturaAgrupamentoResumoResponse(
    string Chave,
    string Label,
    int QuantidadeFaturas,
    decimal ValorTotal);

public sealed record FaturaListSummaryResponse(
    int TotalRegistros,
    decimal ValorTotal,
    IReadOnlyCollection<FaturaAgrupamentoResumoResponse> PorCartao,
    IReadOnlyCollection<FaturaAgrupamentoResumoResponse> PorCompetencia);

public sealed record FaturaListResponse(
    IReadOnlyCollection<FaturaResumoResponse> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    FaturaListSummaryResponse Summary);

public sealed record FaturaItemResponse(
    Guid ContaPagarId,
    string Descricao,
    string RecebedorNome,
    DateOnly DataCompra,
    decimal ValorLiquido,
    string StatusCodigo,
    int NumeroParcela,
    int QuantidadeParcelas);

public sealed record FaturaDetalheResponse(
    Guid Id,
    Guid CartaoId,
    string CartaoNome,
    string Competencia,
    DateOnly DataFechamento,
    DateOnly DataVencimento,
    decimal ValorTotal,
    DateOnly? DataPagamento,
    Guid? ContaBancariaPagamentoId,
    string? ContaBancariaPagamentoNome,
    string StatusCodigo,
    string StatusNome,
    string? Observacao,
    IReadOnlyCollection<FaturaItemResponse> Itens,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
