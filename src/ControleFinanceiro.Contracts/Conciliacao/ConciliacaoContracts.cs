using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Contracts.Filters;

namespace ControleFinanceiro.Contracts.Conciliacao;

public sealed record ConciliacaoListQueryRequest : ListQueryRequest
{
    public string? StatusConciliacaoCodigo { get; init; }

    public Guid? ContaBancariaId { get; init; }
}

public sealed record ConfirmarVinculoConciliacaoRequest(
    Guid MovimentacaoFinanceiraId,
    DateOnly? DataConciliacao,
    string? Observacao);

public sealed record ConciliacaoMovimentacaoCandidataResponse(
    Guid MovimentacaoFinanceiraId,
    DateOnly DataMovimentacao,
    string Tipo,
    string Natureza,
    decimal Valor,
    string StatusCodigo,
    string? Observacao,
    int Score);

public sealed record ConciliacaoItemResponse(
    Guid ItemImportadoWhatsappId,
    Guid ImportacaoWhatsappId,
    string Remetente,
    string? DescricaoExtrato,
    decimal? ValorSugerido,
    DateOnly? DataSugerida,
    string StatusConciliacaoCodigo,
    string StatusConciliacaoNome,
    Guid? MovimentacaoConciliadaId,
    string? MovimentacaoConciliadaDescricao,
    IReadOnlyCollection<ConciliacaoMovimentacaoCandidataResponse> Candidatas);
