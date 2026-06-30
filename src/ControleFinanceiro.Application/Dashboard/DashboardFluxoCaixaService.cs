using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Contracts.Dashboard;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Dashboard;

public interface IDashboardFluxoCaixaService
{
    Task<DashboardFluxoCaixaResponse> ObterFluxoCaixaAsync(DashboardFluxoCaixaQueryRequest query, CancellationToken cancellationToken);
}

public sealed class DashboardFluxoCaixaService(IAppDbContext dbContext, DashboardDbHelpers db) : IDashboardFluxoCaixaService
{
    public async Task<DashboardFluxoCaixaResponse> ObterFluxoCaixaAsync(DashboardFluxoCaixaQueryRequest query, CancellationToken cancellationToken)
    {
        var (dataInicial, dias, mesReferenciaEhMesAtual) = DashboardHelpers.ResolverJanela(query.MesReferencia, query.DataInicial, query.Dias);
        var dataFinal = dataInicial.AddDays(dias - 1);
        var usarDataVencimento = query.Visao == DashboardFluxoCaixaVisao.Caixa;
        var projetarPrevisoes = !mesReferenciaEhMesAtual;

        var saldoInicial = await db.CalcularSaldoRealizadoAteAsync(dataInicial.AddDays(-1), cancellationToken);
        var eventos = new List<FluxoCaixaEvento>();

        var contasPagar = await dbContext.ContasPagar.AsNoTracking()
            .Where(c => c.StatusContaId != StatusConta.LiquidadaId &&
                        c.StatusContaId != StatusConta.CanceladaId &&
                        (usarDataVencimento
                            ? c.DataVencimento >= dataInicial && c.DataVencimento <= dataFinal
                            : c.DataEmissao >= dataInicial && c.DataEmissao <= dataFinal))
            .Select(c => new { c.DataEmissao, c.DataVencimento, c.ValorLiquido })
            .ToListAsync(cancellationToken);

        foreach (var conta in contasPagar)
        {
            var data = usarDataVencimento ? conta.DataVencimento : conta.DataEmissao;
            if (data >= dataInicial && data <= dataFinal)
                eventos.Add(new FluxoCaixaEvento(data, TipoMovimentacao.Saida, conta.ValorLiquido));
        }

        var contasReceber = await dbContext.ContasReceber.AsNoTracking()
            .Where(c => c.StatusContaId != StatusConta.LiquidadaId &&
                        c.StatusContaId != StatusConta.CanceladaId &&
                        (usarDataVencimento
                            ? c.DataVencimento >= dataInicial && c.DataVencimento <= dataFinal
                            : c.DataEmissao >= dataInicial && c.DataEmissao <= dataFinal))
            .Select(c => new { c.DataEmissao, c.DataVencimento, c.ValorLiquido })
            .ToListAsync(cancellationToken);

        foreach (var conta in contasReceber)
        {
            var data = usarDataVencimento ? conta.DataVencimento : conta.DataEmissao;
            if (data >= dataInicial && data <= dataFinal)
                eventos.Add(new FluxoCaixaEvento(data, TipoMovimentacao.Entrada, conta.ValorLiquido));
        }

        var movimentacoes = await dbContext.MovimentacoesFinanceiras.AsNoTracking()
            .Where(m => m.Natureza == NaturezaMovimentacao.Realizada &&
                        m.StatusMovimentacaoId != StatusMovimentacao.CanceladaId &&
                        m.DataMovimentacao >= dataInicial && m.DataMovimentacao <= dataFinal)
            .Select(m => new { m.DataMovimentacao, m.Tipo, m.Valor })
            .ToListAsync(cancellationToken);

        eventos.AddRange(movimentacoes.Select(m => new FluxoCaixaEvento(m.DataMovimentacao, m.Tipo, m.Valor)));

        var comprasImportadas = await db.CarregarComprasImportadasAsync(cancellationToken);

        foreach (var compra in comprasImportadas)
        {
            var data = usarDataVencimento ? compra.DataVencimento : compra.DataCompra;
            if (data >= dataInicial && data <= dataFinal)
                eventos.Add(new FluxoCaixaEvento(data, compra.Tipo, compra.Valor));
        }

        if (projetarPrevisoes)
        {
            var recorrencias = await db.ProjetarRecorrenciasAsync(dataInicial, dataFinal, cancellationToken);
            eventos.AddRange(recorrencias.Select(r => new FluxoCaixaEvento(r.Data, r.Tipo, r.Valor)));

            eventos.AddRange(DashboardHelpers.ProjetarComprasImportadas(comprasImportadas, dataInicial, dataFinal, usarDataVencimento)
                .Select(p => new FluxoCaixaEvento(p.Data, p.Compra.Tipo, p.Compra.Valor)));
        }

        var fluxo = FluxoCaixaDiario.Projetar(dataInicial, dias, saldoInicial, eventos);

        return new DashboardFluxoCaixaResponse(
            query.Visao, dataInicial, dias,
            fluxo.Any(dia => dia.RiscoSaldoNegativo),
            fluxo.Select(dia => new DashboardFluxoCaixaDiaResponse(
                dia.Data, dia.SaldoInicial, dia.EntradasPrevistas,
                dia.SaidasPrevistas, dia.SaldoFinalPrevisto, dia.RiscoSaldoNegativo)).ToList());
    }
}
