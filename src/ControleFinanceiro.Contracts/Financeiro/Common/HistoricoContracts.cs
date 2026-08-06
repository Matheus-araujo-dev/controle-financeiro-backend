namespace ControleFinanceiro.Contracts.Financeiro.Common;

public sealed record AlteracaoCampoResponse(string Campo, string? Antes, string? Depois);

public sealed record HistoricoEntradaResponse(
    Guid Id,
    string Acao,
    string RealizadoPor,
    DateTime OcorreuEmUtc,
    IReadOnlyList<AlteracaoCampoResponse> Alteracoes);
