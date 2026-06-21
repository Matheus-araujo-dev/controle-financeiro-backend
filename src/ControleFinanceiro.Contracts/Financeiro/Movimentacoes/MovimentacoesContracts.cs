using ControleFinanceiro.Contracts.Filters;

namespace ControleFinanceiro.Contracts.Financeiro.Movimentacoes;

public enum TipoMovimentacaoResponse
{
    Entrada = 1,
    Saida = 2
}

public enum NaturezaMovimentacaoResponse
{
    Prevista = 1,
    Realizada = 2,
    Economica = 3
}

public sealed record MovimentacaoListQueryRequest : ListQueryRequest
{
    public Guid? ContaBancariaId { get; init; }

    public string? ContaBancariaIds { get; init; }

    public string? ResponsavelIds { get; init; }

    public string? StatusCodigo { get; init; }

    public TipoMovimentacaoResponse? Tipo { get; init; }

    public NaturezaMovimentacaoResponse? Natureza { get; init; }

    public DateOnly? DataInicial { get; init; }

    public DateOnly? DataFinal { get; init; }
}
public sealed record MovimentacaoResumoResponse(
    Guid Id,
    DateOnly DataMovimentacao,
    TipoMovimentacaoResponse Tipo,
    NaturezaMovimentacaoResponse Natureza,
    string StatusCodigo,
    string StatusNome,
    decimal Valor,
    Guid? ContaBancariaId,
    string? ContaBancariaNome,
    Guid? ContaPagarId,
    Guid? ContaReceberId,
    Guid? FaturaCartaoId,
    string? Observacao,
    string? ResponsavelNome);

public sealed record MovimentacaoListSummaryResponse(
    int TotalRegistros,
    decimal TotalEntradas,
    decimal TotalSaidas,
    decimal SaldoLiquido);

public sealed record MovimentacaoListResponse(
    IReadOnlyCollection<MovimentacaoResumoResponse> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    MovimentacaoListSummaryResponse Summary);

public sealed record MovimentacaoDetalheResponse(
    Guid Id,
    DateOnly DataMovimentacao,
    TipoMovimentacaoResponse Tipo,
    NaturezaMovimentacaoResponse Natureza,
    string StatusCodigo,
    string StatusNome,
    decimal Valor,
    Guid? ContaBancariaId,
    string? ContaBancariaNome,
    Guid? ContaPagarId,
    Guid? ContaReceberId,
    Guid? FaturaCartaoId,
    string? Observacao,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
