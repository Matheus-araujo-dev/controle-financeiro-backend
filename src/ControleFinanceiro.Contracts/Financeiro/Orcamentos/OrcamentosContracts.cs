namespace ControleFinanceiro.Contracts.Financeiro.Orcamentos;

public sealed record OrcamentoQueryRequest
{
    public string? Competencia { get; init; }
}

public sealed record UpsertMetaOrcamentoRequest(
    Guid ContaGerencialId,
    string Competencia,
    decimal ValorMeta);

public sealed record MetaOrcamentoResponse(
    Guid Id,
    Guid ContaGerencialId,
    string Competencia,
    decimal ValorMeta);

public sealed record OrcamentoCompetenciaResponse(
    string Competencia,
    decimal TotalMeta,
    decimal TotalRealizado,
    decimal? PercentualConsumido,
    bool PossuiEstouro,
    IReadOnlyCollection<OrcamentoItemResponse> Itens);

public sealed record OrcamentoItemResponse(
    Guid? MetaId,
    Guid ContaGerencialId,
    Guid? ContaPaiId,
    string? ContaGerencialCodigo,
    string ContaGerencialDescricao,
    decimal? ValorMeta,
    decimal ValorRealizado,
    decimal? PercentualConsumido,
    bool Estourado,
    bool AceitaLancamentos);
