using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Contracts.Dashboard;
using ControleFinanceiro.Contracts.Financeiro.Movimentacoes;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Dashboard;

public sealed class DashboardAppService(IAppDbContext dbContext)
{
    private const int MaxItemsPerList = 5;
    private const int DefaultProjectionDays = 15;
    private const int MaxProjectionDays = 60;

    public async Task<DashboardResumoResponse> ObterResumoAsync(
        DashboardResumoQueryRequest query,
        CancellationToken cancellationToken)
    {
        var dataReferencia = query.DataReferencia ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var diasProjetados = NormalizarDias(query.DiasProjetados);
        var fluxo = await ObterFluxoCaixaInternoAsync(dataReferencia, diasProjetados, DashboardFluxoCaixaVisao.Caixa, cancellationToken);
        var contasPagarAbertas = await CarregarContasPagarAbertasAsync(cancellationToken);
        var contasReceberAbertas = await CarregarContasReceberAbertasAsync(cancellationToken);
        var saldoAtual = fluxo.Itens.FirstOrDefault()?.SaldoInicial ?? 0m;
        var saldoProjetado = fluxo.Itens.LastOrDefault()?.SaldoFinalPrevisto ?? saldoAtual;

        return new DashboardResumoResponse(
            saldoAtual,
            decimal.Round(contasPagarAbertas.Sum(x => x.Valor), 2, MidpointRounding.AwayFromZero),
            decimal.Round(contasReceberAbertas.Sum(x => x.Valor), 2, MidpointRounding.AwayFromZero),
            saldoProjetado,
            fluxo.RiscoSaldoNegativo,
            MapearContasResumo(contasPagarAbertas, contasReceberAbertas, dataReferencia, vencidas: true),
            MapearContasResumo(contasPagarAbertas, contasReceberAbertas, dataReferencia, vencidas: false),
            await CarregarMovimentacoesRecentesAsync(cancellationToken));
    }

    public async Task<DashboardFluxoCaixaResponse> ObterFluxoCaixaAsync(
        DashboardFluxoCaixaQueryRequest query,
        CancellationToken cancellationToken)
    {
        var dataInicial = query.DataInicial ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var dias = NormalizarDias(query.Dias);
        return await ObterFluxoCaixaInternoAsync(dataInicial, dias, query.Visao, cancellationToken);
    }

    private async Task<DashboardFluxoCaixaResponse> ObterFluxoCaixaInternoAsync(
        DateOnly dataInicial,
        int dias,
        DashboardFluxoCaixaVisao visao,
        CancellationToken cancellationToken)
    {
        var saldoBase = await CalcularSaldoBaseAsync(dataInicial.AddDays(-1), visao, cancellationToken);
        var eventos = await CarregarEventosFluxoAsync(dataInicial, dias, visao, cancellationToken);
        var itens = FluxoCaixaDiario.Projetar(dataInicial, dias, saldoBase, eventos)
            .Select(item => new DashboardFluxoCaixaDiaResponse(
                item.Data,
                item.SaldoInicial,
                item.EntradasPrevistas,
                item.SaidasPrevistas,
                item.SaldoFinalPrevisto,
                item.RiscoSaldoNegativo))
            .ToArray();

        return new DashboardFluxoCaixaResponse(
            visao,
            dataInicial,
            dias,
            itens.Any(item => item.RiscoSaldoNegativo),
            itens);
    }

    private async Task<decimal> CalcularSaldoBaseAsync(
        DateOnly dataCorte,
        DashboardFluxoCaixaVisao visao,
        CancellationToken cancellationToken)
    {
        var saldoContas = await dbContext.ContasBancarias
            .AsNoTracking()
            .Where(x => x.DataSaldoInicial <= dataCorte)
            .SumAsync(x => (decimal?)x.SaldoInicial, cancellationToken) ?? 0m;

        var movimentosRealizados = await dbContext.MovimentacoesFinanceiras
            .AsNoTracking()
            .Where(x =>
                x.StatusMovimentacaoId != StatusMovimentacao.CanceladaId &&
                x.Natureza == NaturezaMovimentacao.Realizada &&
                x.DataMovimentacao <= dataCorte &&
                (visao == DashboardFluxoCaixaVisao.Caixa || x.FaturaCartaoId == null))
            .Select(x => new { x.Tipo, x.Valor })
            .ToArrayAsync(cancellationToken);

        var saldo = saldoContas + movimentosRealizados.Sum(CalcularImpacto);

        if (visao == DashboardFluxoCaixaVisao.Economica)
        {
            var movimentosEconomicos = await dbContext.MovimentacoesFinanceiras
                .AsNoTracking()
                .Where(x =>
                    x.StatusMovimentacaoId != StatusMovimentacao.CanceladaId &&
                    x.Natureza == NaturezaMovimentacao.Economica &&
                    x.DataMovimentacao <= dataCorte)
                .Select(x => new { x.Tipo, x.Valor })
                .ToArrayAsync(cancellationToken);

            saldo += movimentosEconomicos.Sum(CalcularImpacto);
        }

        return decimal.Round(saldo, 2, MidpointRounding.AwayFromZero);
    }

    private async Task<IReadOnlyCollection<FluxoCaixaEvento>> CarregarEventosFluxoAsync(
        DateOnly dataInicial,
        int dias,
        DashboardFluxoCaixaVisao visao,
        CancellationToken cancellationToken)
    {
        var dataFinal = dataInicial.AddDays(dias - 1);
        var eventos = new List<FluxoCaixaEvento>();
        var contasPagarAbertas = await CarregarContasPagarAbertasAsync(cancellationToken);
        var contasReceberAbertas = await CarregarContasReceberAbertasAsync(cancellationToken);

        foreach (var conta in contasReceberAbertas)
        {
            AdicionarEventoNoIntervalo(eventos, conta.DataVencimento, dataInicial, dataFinal, TipoMovimentacao.Entrada, conta.Valor);
        }

        foreach (var conta in contasPagarAbertas.Where(x => !x.EhCartao))
        {
            AdicionarEventoNoIntervalo(eventos, conta.DataVencimento, dataInicial, dataFinal, TipoMovimentacao.Saida, conta.Valor);
        }

        if (visao == DashboardFluxoCaixaVisao.Caixa)
        {
            var comprasCartaoAbertas = await CarregarComprasCartaoAbertasAsync(cancellationToken);

            foreach (var compra in comprasCartaoAbertas)
            {
                var competencia = FaturaCartaoCompetencia.Calcular(
                    compra.DataEmissao,
                    compra.DiaFechamentoFatura,
                    compra.DiaVencimentoFatura);

                AdicionarEventoNoIntervalo(
                    eventos,
                    competencia.DataVencimento,
                    dataInicial,
                    dataFinal,
                    TipoMovimentacao.Saida,
                    compra.Valor);
            }
        }
        else
        {
            var movimentosEconomicos = await dbContext.MovimentacoesFinanceiras
                .AsNoTracking()
                .Where(x =>
                    x.StatusMovimentacaoId != StatusMovimentacao.CanceladaId &&
                    x.Natureza == NaturezaMovimentacao.Economica &&
                    x.DataMovimentacao >= dataInicial &&
                    x.DataMovimentacao <= dataFinal)
                .Select(x => new FluxoCaixaEvento(x.DataMovimentacao, x.Tipo, x.Valor))
                .ToArrayAsync(cancellationToken);

            eventos.AddRange(movimentosEconomicos);
        }

        return eventos;
    }

    private async Task<IReadOnlyCollection<DashboardContaProjection>> CarregarContasPagarAbertasAsync(
        CancellationToken cancellationToken)
    {
        return await (
            from conta in dbContext.ContasPagar.AsNoTracking()
            join pessoa in dbContext.Pessoas.AsNoTracking() on conta.RecebedorId equals pessoa.Id
            join status in dbContext.StatusContas.AsNoTracking() on conta.StatusContaId equals status.Id
            join forma in dbContext.FormasPagamento.AsNoTracking() on conta.FormaPagamentoId equals forma.Id
            where conta.StatusContaId != StatusConta.LiquidadaId &&
                  conta.StatusContaId != StatusConta.CanceladaId
            select new DashboardContaProjection(
                conta.Id,
                "ContaPagar",
                conta.Descricao,
                pessoa.Nome,
                conta.DataEmissao,
                conta.DataVencimento,
                conta.ValorLiquido,
                status.Codigo,
                status.Nome,
                forma.EhCartao))
            .ToArrayAsync(cancellationToken);
    }

    private async Task<IReadOnlyCollection<DashboardContaProjection>> CarregarContasReceberAbertasAsync(
        CancellationToken cancellationToken)
    {
        return await (
            from conta in dbContext.ContasReceber.AsNoTracking()
            join pessoa in dbContext.Pessoas.AsNoTracking() on conta.PagadorId equals pessoa.Id
            join status in dbContext.StatusContas.AsNoTracking() on conta.StatusContaId equals status.Id
            where conta.StatusContaId != StatusConta.LiquidadaId &&
                  conta.StatusContaId != StatusConta.CanceladaId
            select new DashboardContaProjection(
                conta.Id,
                "ContaReceber",
                conta.Descricao,
                pessoa.Nome,
                conta.DataEmissao,
                conta.DataVencimento,
                conta.ValorLiquido,
                status.Codigo,
                status.Nome,
                false))
            .ToArrayAsync(cancellationToken);
    }

    private async Task<IReadOnlyCollection<CompraCartaoProjection>> CarregarComprasCartaoAbertasAsync(
        CancellationToken cancellationToken)
    {
        return await (
            from conta in dbContext.ContasPagar.AsNoTracking()
            join cartao in dbContext.Cartoes.AsNoTracking() on conta.CartaoId equals cartao.Id
            join forma in dbContext.FormasPagamento.AsNoTracking() on conta.FormaPagamentoId equals forma.Id
            where conta.CartaoId.HasValue &&
                  forma.EhCartao &&
                  conta.StatusContaId != StatusConta.LiquidadaId &&
                  conta.StatusContaId != StatusConta.CanceladaId
            select new CompraCartaoProjection(
                conta.Id,
                conta.DataEmissao,
                conta.ValorLiquido,
                cartao.DiaFechamentoFatura,
                cartao.DiaVencimentoFatura))
            .ToArrayAsync(cancellationToken);
    }

    private async Task<IReadOnlyCollection<DashboardMovimentacaoResumoResponse>> CarregarMovimentacoesRecentesAsync(
        CancellationToken cancellationToken)
    {
        var itens = await dbContext.MovimentacoesFinanceiras
            .AsNoTracking()
            .Where(x => x.StatusMovimentacaoId != StatusMovimentacao.CanceladaId)
            .OrderByDescending(x => x.DataMovimentacao)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Take(MaxItemsPerList)
            .Select(x => new
            {
                x.Id,
                x.DataMovimentacao,
                x.Tipo,
                x.Natureza,
                x.Valor,
                x.Observacao,
                x.ContaPagarId,
                x.ContaReceberId,
                x.FaturaCartaoId
            })
            .ToArrayAsync(cancellationToken);

        return itens
            .Select(item => new DashboardMovimentacaoResumoResponse(
                item.Id,
                item.DataMovimentacao,
                MapearTipo(item.Tipo),
                MapearNatureza(item.Natureza),
                item.Valor,
                item.Observacao,
                item.ContaPagarId,
                item.ContaReceberId,
                item.FaturaCartaoId))
            .ToArray();
    }

    private static IReadOnlyCollection<DashboardContaResumoResponse> MapearContasResumo(
        IReadOnlyCollection<DashboardContaProjection> contasPagarAbertas,
        IReadOnlyCollection<DashboardContaProjection> contasReceberAbertas,
        DateOnly dataReferencia,
        bool vencidas)
    {
        var contas = contasPagarAbertas
            .Concat(contasReceberAbertas)
            .Where(conta => vencidas ? conta.DataVencimento < dataReferencia : conta.DataVencimento >= dataReferencia)
            .OrderBy(conta => conta.DataVencimento)
            .ThenBy(conta => conta.Descricao)
            .Take(MaxItemsPerList)
            .Select(conta =>
            {
                var status = conta.DataVencimento < dataReferencia
                    ? ("VENCIDA", "Vencida")
                    : (conta.StatusCodigo, conta.StatusNome);

                return new DashboardContaResumoResponse(
                    conta.Id,
                    conta.TipoLancamento,
                    conta.Descricao,
                    conta.PessoaNome,
                    conta.DataVencimento,
                    conta.Valor,
                    status.Item1,
                    status.Item2);
            })
            .ToArray();

        return contas;
    }

    private static void AdicionarEventoNoIntervalo(
        ICollection<FluxoCaixaEvento> eventos,
        DateOnly dataOriginal,
        DateOnly dataInicial,
        DateOnly dataFinal,
        TipoMovimentacao tipo,
        decimal valor)
    {
        if (dataOriginal > dataFinal)
        {
            return;
        }

        var dataEvento = dataOriginal < dataInicial ? dataInicial : dataOriginal;
        eventos.Add(new FluxoCaixaEvento(dataEvento, tipo, valor));
    }

    private static int NormalizarDias(int dias)
    {
        var valor = dias <= 0 ? DefaultProjectionDays : dias;
        return Math.Clamp(valor, 1, MaxProjectionDays);
    }

    private static decimal CalcularImpacto(dynamic movimento)
    {
        return movimento.Tipo == TipoMovimentacao.Entrada ? movimento.Valor : -movimento.Valor;
    }

    private static TipoMovimentacaoResponse MapearTipo(TipoMovimentacao tipo)
    {
        return tipo switch
        {
            TipoMovimentacao.Entrada => TipoMovimentacaoResponse.Entrada,
            TipoMovimentacao.Saida => TipoMovimentacaoResponse.Saida,
            _ => throw new ArgumentOutOfRangeException(nameof(tipo))
        };
    }

    private static NaturezaMovimentacaoResponse MapearNatureza(NaturezaMovimentacao natureza)
    {
        return natureza switch
        {
            NaturezaMovimentacao.Prevista => NaturezaMovimentacaoResponse.Prevista,
            NaturezaMovimentacao.Realizada => NaturezaMovimentacaoResponse.Realizada,
            NaturezaMovimentacao.Economica => NaturezaMovimentacaoResponse.Economica,
            _ => throw new ArgumentOutOfRangeException(nameof(natureza))
        };
    }

    private sealed record DashboardContaProjection(
        Guid Id,
        string TipoLancamento,
        string Descricao,
        string PessoaNome,
        DateOnly DataEmissao,
        DateOnly DataVencimento,
        decimal Valor,
        string StatusCodigo,
        string StatusNome,
        bool EhCartao);

    private sealed record CompraCartaoProjection(
        Guid ContaPagarId,
        DateOnly DataEmissao,
        decimal Valor,
        int DiaFechamentoFatura,
        int DiaVencimentoFatura);
}
