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
    string? Observacao);

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
    DateOnly? DataConciliacao,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
