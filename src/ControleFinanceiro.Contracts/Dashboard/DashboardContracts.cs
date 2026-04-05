using ControleFinanceiro.Contracts.Financeiro.Movimentacoes;

namespace ControleFinanceiro.Contracts.Dashboard;

public enum DashboardFluxoCaixaVisao
{
    Caixa = 1,
    Economica = 2
}

public sealed record DashboardResumoQueryRequest
{
    public DateOnly? DataReferencia { get; init; }

    public int DiasProjetados { get; init; } = 15;
}

public sealed record DashboardFluxoCaixaQueryRequest
{
    public DateOnly? DataInicial { get; init; }

    public int Dias { get; init; } = 15;

    public DashboardFluxoCaixaVisao Visao { get; init; } = DashboardFluxoCaixaVisao.Caixa;
}

public sealed record DashboardResumoResponse(
    decimal SaldoAtual,
    decimal TotalAPagar,
    decimal TotalAReceber,
    decimal SaldoProjetado,
    bool RiscoSaldoNegativo,
    IReadOnlyCollection<DashboardContaResumoResponse> ContasVencidas,
    IReadOnlyCollection<DashboardContaResumoResponse> ContasAVencer,
    IReadOnlyCollection<DashboardMovimentacaoResumoResponse> MovimentacoesRecentes);

public sealed record DashboardContaResumoResponse(
    Guid Id,
    string TipoLancamento,
    string Descricao,
    string PessoaNome,
    DateOnly DataVencimento,
    decimal Valor,
    string StatusCodigo,
    string StatusNome);

public sealed record DashboardMovimentacaoResumoResponse(
    Guid Id,
    DateOnly DataMovimentacao,
    TipoMovimentacaoResponse Tipo,
    NaturezaMovimentacaoResponse Natureza,
    decimal Valor,
    string? Observacao,
    Guid? ContaPagarId,
    Guid? ContaReceberId,
    Guid? FaturaCartaoId);

public sealed record DashboardFluxoCaixaResponse(
    DashboardFluxoCaixaVisao Visao,
    DateOnly DataInicial,
    int Dias,
    bool RiscoSaldoNegativo,
    IReadOnlyCollection<DashboardFluxoCaixaDiaResponse> Itens);

public sealed record DashboardFluxoCaixaDiaResponse(
    DateOnly Data,
    decimal SaldoInicial,
    decimal EntradasPrevistas,
    decimal SaidasPrevistas,
    decimal SaldoFinalPrevisto,
    bool RiscoSaldoNegativo);
