namespace ControleFinanceiro.Contracts.Dashboard;

public sealed record DashboardAnomaliasResponse(
    string MesReferencia, DateOnly HistoricoInicial, bool Completo, int ContasAnalisadas,
    IReadOnlyList<DashboardAnomaliaItem> Itens);

public sealed record DashboardAnomaliaItem(
    string Id, string Tipo, string Descricao, string Regra, decimal ValorAtual,
    decimal? ValorBase, IReadOnlyList<DashboardAnomaliaEvidencia> Evidencias);

public sealed record DashboardAnomaliaEvidencia(Guid ContaPagarId, DateOnly Data, decimal Valor);
