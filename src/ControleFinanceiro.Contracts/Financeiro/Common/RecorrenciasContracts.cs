namespace ControleFinanceiro.Contracts.Financeiro.Common;

public enum TipoPeriodicidadeRecorrencia
{
    Mensal = 1
}

public sealed record RecorrenciaConfigRequest(
    TipoPeriodicidadeRecorrencia TipoPeriodicidade,
    int DiaGeracaoMensal,
    DateOnly DataInicio,
    DateOnly? DataFim,
    bool PermiteEdicaoOcorrenciaIndividual,
    string? Observacao);

public sealed record RecorrenciaResponse(
    Guid Id,
    TipoPeriodicidadeRecorrencia TipoPeriodicidade,
    int DiaGeracaoMensal,
    DateOnly DataInicio,
    DateOnly? DataFim,
    bool Ativa,
    bool PermiteEdicaoOcorrenciaIndividual,
    string? Observacao);

public sealed record GerarOcorrenciasRecorrenciaRequest(DateOnly AteData);

public sealed record EncerrarRecorrenciaRequest(DateOnly DataFim);
