namespace ControleFinanceiro.Contracts.Conciliacao;

public sealed record IniciarConciliacaoRequest(
    Guid ContaBancariaId);

public sealed record ConciliacaoResumoResponse(
    Guid Id,
    string NomeArquivo,
    string Formato,
    Guid ContaBancariaId,
    DateOnly DataInicio,
    DateOnly DataFim,
    int TotalItens,
    int ItensConciliados,
    string Status,
    DateTime CriadoEmUtc);

public sealed record ItemConciliacaoResponse(
    Guid Id,
    DateOnly Data,
    string Descricao,
    decimal Valor,
    string? Documento,
    string Status,
    Guid? MovimentacaoVinculadaId,
    Guid? SugestaoMovimentacaoId,
    decimal? ScoreSugestao);

public sealed record ConciliacaoDetalheResponse(
    Guid Id,
    string NomeArquivo,
    string Formato,
    Guid ContaBancariaId,
    DateOnly DataInicio,
    DateOnly DataFim,
    int TotalItens,
    int ItensConciliados,
    string Status,
    IReadOnlyCollection<ItemConciliacaoResponse> Itens);

public sealed record ConciliarItemRequest(
    Guid? MovimentacaoId);