namespace ControleFinanceiro.Contracts.Financeiro.Fechamentos;

public sealed record ReabrirFechamentoMensalRequest(string Justificativa);

public sealed record FechamentoMensalItemResponse(
    string Id,
    string Titulo,
    string Descricao,
    string Status,
    bool Bloqueante,
    int Quantidade,
    decimal? Valor,
    string RotaAcao);

public sealed record FechamentoMensalResponse(
    string Competencia,
    string Status,
    bool ProntoParaFechar,
    int QuantidadeBloqueios,
    decimal TotalReceitas,
    decimal TotalDespesas,
    decimal Saldo,
    decimal TotalPendente,
    decimal TotalVencido,
    int QuantidadeLancamentos,
    int QuantidadeSemCategoria,
    int QuantidadeSemResponsavel,
    int QuantidadeConciliacoesPendentes,
    Guid? FechadoPorUsuarioId,
    DateTimeOffset? FechadoEmUtc,
    Guid? ReabertoPorUsuarioId,
    DateTimeOffset? ReabertoEmUtc,
    string? JustificativaReabertura,
    IReadOnlyCollection<FechamentoMensalItemResponse> Itens);
