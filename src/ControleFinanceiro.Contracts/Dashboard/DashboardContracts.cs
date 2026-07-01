using ControleFinanceiro.Contracts.Financeiro.Movimentacoes;

namespace ControleFinanceiro.Contracts.Dashboard;

public enum DashboardFluxoCaixaVisao
{
    Caixa = 1,
    Economica = 2
}

public enum DashboardCentralPrevisaoOrigem
{
    Recorrencia = 1,
    Parcela = 2,
    CompraRecorrenteImportada = 3,
    CompraPlanejada = 4,
    ContaFuturaGerada = 5
}

public enum DashboardCentralPrevisaoStatus
{
    Realizado = 1,
    Previsto = 2,
    Substituido = 3
}

public sealed record DashboardResumoQueryRequest
{
    public string? MesReferencia { get; init; }

    public DateOnly? DataReferencia { get; init; }

    public int DiasProjetados { get; init; } = 15;
}

public sealed record DashboardFluxoCaixaQueryRequest
{
    public string? MesReferencia { get; init; }

    public DateOnly? DataInicial { get; init; }

    public int Dias { get; init; } = 15;

    public DashboardFluxoCaixaVisao Visao { get; init; } = DashboardFluxoCaixaVisao.Caixa;
}

public sealed record DashboardContaGerencialResumoQueryRequest
{
    public string? MesReferencia { get; init; }

    public DateOnly? DataInicial { get; init; }

    public int Dias { get; init; } = 30;

    public string? Tipo { get; init; }
}

public sealed record DashboardContaGerencialSerieQueryRequest
{
    public string? MesReferencia { get; init; }

    public DateOnly? DataInicial { get; init; }

    public int Dias { get; init; } = 30;

    public string? Tipo { get; init; }

    public Guid? ContaGerencialId { get; init; }
}

public sealed record DashboardContaGerencialLancamentosQueryRequest
{
    public string? MesReferencia { get; init; }

    public DateOnly? DataInicial { get; init; }

    public int Dias { get; init; } = 30;

    public string? Tipo { get; init; }

    public Guid? ContaGerencialId { get; init; }
}

public sealed record DashboardCentralPrevisaoQueryRequest
{
    public string? MesReferencia { get; init; }

    public DateOnly? DataInicial { get; init; }

    public int Dias { get; init; } = 30;

    public DashboardCentralPrevisaoOrigem? Origem { get; init; }

    public DashboardCentralPrevisaoStatus? Status { get; init; }
}

public sealed record DashboardCentralPrevisaoItensQueryRequest
{
    public string? MesReferencia { get; init; }

    public DateOnly? DataInicial { get; init; }

    public int Dias { get; init; } = 30;

    public DateOnly? Data { get; init; }

    public DashboardCentralPrevisaoOrigem? Origem { get; init; }

    public DashboardCentralPrevisaoStatus? Status { get; init; }
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
    string? ObservacaoResumida,
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

public sealed record DashboardContaGerencialResumoResponse(
    DateOnly DataInicial,
    int Dias,
    decimal TotalReceitas,
    decimal TotalDespesas,
    decimal Saldo,
    IReadOnlyCollection<DashboardContaGerencialResumoItemResponse> Itens);

public sealed record DashboardContaGerencialResumoItemResponse(
    Guid ContaGerencialId,
    string? Codigo,
    string Descricao,
    string Tipo,
    decimal ValorTotal,
    int QuantidadeLancamentos,
    DateOnly UltimaDataLancamento);

public sealed record DashboardContaGerencialSerieResponse(
    DateOnly DataInicial,
    int Dias,
    string? Tipo,
    Guid? ContaGerencialId,
    IReadOnlyCollection<DashboardContaGerencialSerieDiaResponse> Itens);

public sealed record DashboardContaGerencialSerieDiaResponse(
    DateOnly Data,
    decimal TotalReceitas,
    decimal TotalDespesas,
    decimal Saldo);

public sealed record DashboardContaGerencialLancamentosResponse(
    DateOnly DataInicial,
    int Dias,
    string Tipo,
    Guid ContaGerencialId,
    string? ContaGerencialCodigo,
    string ContaGerencialDescricao,
    IReadOnlyCollection<DashboardContaGerencialLancamentoItemResponse> Itens);

public sealed record DashboardContaGerencialLancamentoItemResponse(
    Guid LancamentoId,
    string TipoLancamento,
    string Descricao,
    string PessoaNome,
    DateOnly DataEmissao,
    DateOnly DataVencimento,
    decimal ValorLancamento,
    decimal ValorRateio,
    string StatusCodigo,
    string StatusNome);

public sealed record DashboardCentralPrevisaoResumoResponse(
    DateOnly DataInicial,
    int Dias,
    DashboardCentralPrevisaoOrigem? Origem,
    DashboardCentralPrevisaoStatus? Status,
    IReadOnlyCollection<DashboardCentralPrevisaoResumoItemResponse> Itens);

public sealed record DashboardCentralPrevisaoResumoItemResponse(
    DateOnly Data,
    TipoMovimentacaoResponse TipoMovimentacao,
    DashboardCentralPrevisaoOrigem Origem,
    DashboardCentralPrevisaoStatus Status,
    int QuantidadeItens,
    decimal ValorTotal);

public sealed record DashboardCentralPrevisaoItensResponse(
    DateOnly DataInicial,
    int Dias,
    DateOnly? Data,
    DashboardCentralPrevisaoOrigem? Origem,
    DashboardCentralPrevisaoStatus? Status,
    IReadOnlyCollection<DashboardCentralPrevisaoItemResponse> Itens);

public sealed record DashboardCentralPrevisaoItemResponse(
    string TipoReferencia,
    Guid ReferenciaId,
    DateOnly Data,
    TipoMovimentacaoResponse TipoMovimentacao,
    DashboardCentralPrevisaoOrigem Origem,
    DashboardCentralPrevisaoStatus Status,
    string Descricao,
    decimal Valor,
    string? PessoaNome,
    string? ResponsavelNome,
    Guid? ContaGerencialId,
    string? ContaGerencialCodigo,
    string? ContaGerencialDescricao);

public sealed record DashboardResponsavelQueryRequest
{
    public string? MesReferencia { get; init; }
    public DateOnly? DataInicial { get; init; }
    public int Dias { get; init; } = 30;
}

public sealed record DashboardResponsavelItemResponse(
    Guid? ResponsavelId,
    string ResponsavelNome,
    decimal TotalDespesas,
    decimal TotalDespesasCartao,
    decimal TotalReceitas,
    decimal SaldoLiquido,
    int QuantidadeLancamentos);

public sealed record DashboardResponsavelResumoResponse(
    DateOnly DataInicial,
    int Dias,
    decimal TotalDespesas,
    decimal TotalReceitas,
    IReadOnlyList<DashboardResponsavelItemResponse> Itens);

public sealed record DashboardComparativoMensalQueryRequest
{
    public int Meses { get; init; } = 6;
}

public sealed record DashboardComparativoMensalItemResponse(
    string Competencia,
    string CompetenciaLabel,
    decimal Receitas,
    decimal Despesas,
    decimal Saldo,
    decimal? VariacaoReceitas,
    decimal? VariacaoDespesas);

public sealed record DashboardComparativoMensalResponse(
    IReadOnlyList<DashboardComparativoMensalItemResponse> Itens);
