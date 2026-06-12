namespace ControleFinanceiro.Contracts.Financeiro.Common;

public enum TipoPeriodicidadeRecorrencia
{
    Mensal = 1
}

public enum TipoDiaRecorrencia
{
    DiaFixo = 1,
    DiaUtil = 2
}

public sealed record RecorrenciaConfigRequest(
    TipoPeriodicidadeRecorrencia TipoPeriodicidade,
    TipoDiaRecorrencia TipoDia,
    int DiaOrdemMensal,
    DateOnly? DataInicio,
    DateOnly? DataFim,
    bool PermiteEdicaoOcorrenciaIndividual,
    string? Observacao);

public sealed record RecorrenciaResponse(
    Guid Id,
    TipoPeriodicidadeRecorrencia TipoPeriodicidade,
    TipoDiaRecorrencia TipoDia,
    int DiaOrdemMensal,
    DateOnly DataInicio,
    DateOnly? DataFim,
    bool Ativa,
    bool PermiteEdicaoOcorrenciaIndividual,
    string? Observacao);

public sealed record RecorrenciaListItemResponse(
    Guid Id,
    TipoPeriodicidadeRecorrencia TipoPeriodicidade,
    TipoDiaRecorrencia TipoDia,
    int DiaOrdemMensal,
    DateOnly DataInicio,
    DateOnly? DataFim,
    bool Ativa,
    bool PermiteEdicaoOcorrenciaIndividual,
    string? Observacao,
    string ContaOrigemTipo,
    Guid ContaOrigemId,
    string Descricao,
    decimal ValorLiquido,
    string PessoaNome,
    string? ResponsavelNome);

public sealed record RecorrenciaListSummaryResponse(
    int TotalRegistros,
    decimal ValorTotal);

public sealed record RecorrenciaListResponse(
    IReadOnlyCollection<RecorrenciaListItemResponse> Items,
    RecorrenciaListSummaryResponse Summary);

public sealed record GerarOcorrenciasRecorrenciaRequest(DateOnly AteData);

public sealed record EncerrarRecorrenciaRequest(DateOnly DataFim);
