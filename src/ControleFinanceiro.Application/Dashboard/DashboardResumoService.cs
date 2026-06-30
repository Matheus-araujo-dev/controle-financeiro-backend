using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Contracts.Dashboard;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Dashboard;

public interface IDashboardResumoService
{
    Task<DashboardResumoResponse> ObterResumoAsync(DashboardResumoQueryRequest query, CancellationToken cancellationToken);
}

public sealed class DashboardResumoService(IAppDbContext dbContext, DashboardDbHelpers db) : IDashboardResumoService
{
    public async Task<DashboardResumoResponse> ObterResumoAsync(DashboardResumoQueryRequest query, CancellationToken cancellationToken)
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        DateOnly dataReferencia;
        DateOnly dataFinal;

        if (!string.IsNullOrWhiteSpace(query.MesReferencia))
        {
            var inicioMes = DashboardHelpers.ParseMesReferencia(query.MesReferencia);
            var fimMes = inicioMes.AddMonths(1).AddDays(-1);
            dataReferencia = hoje >= inicioMes && hoje <= fimMes ? hoje : inicioMes;
            dataFinal = fimMes;
        }
        else
        {
            dataReferencia = query.DataReferencia ?? hoje;
            dataFinal = dataReferencia.AddDays(query.DiasProjetados);
        }

        var pessoas = await db.CarregarPessoasAsync(cancellationToken);

        var saldoAtual = await db.CalcularSaldoRealizadoAteAsync(hoje, cancellationToken);
        var totalAPagar = await CalcularTotalPendenteContasPagarAsync(dataFinal, cancellationToken);
        var totalAReceber = await CalcularTotalPendenteContasReceberAsync(dataFinal, cancellationToken);

        var contasVencidas = await CarregarContasVencidasAsync(dataReferencia, pessoas, cancellationToken);
        var contasAVencer = await CarregarContasAVencerAsync(dataReferencia, dataFinal, pessoas, cancellationToken);
        var movimentacoesRecentes = await CarregarMovimentacoesRecentesAsync(dataReferencia, cancellationToken);

        var saldoProjetado = decimal.Round(saldoAtual + totalAReceber - totalAPagar, 2, MidpointRounding.AwayFromZero);

        return new DashboardResumoResponse(
            saldoAtual, totalAPagar, totalAReceber,
            saldoProjetado, saldoProjetado < 0,
            contasVencidas, contasAVencer, movimentacoesRecentes);
    }

    private async Task<decimal> CalcularTotalPendenteContasPagarAsync(DateOnly dataFinal, CancellationToken cancellationToken) =>
        await dbContext.ContasPagar.AsNoTracking()
            .Where(c => c.StatusContaId != StatusConta.LiquidadaId &&
                        c.StatusContaId != StatusConta.CanceladaId &&
                        c.DataVencimento <= dataFinal)
            .SumAsync(c => (decimal?)c.ValorLiquido, cancellationToken) ?? 0m;

    private async Task<decimal> CalcularTotalPendenteContasReceberAsync(DateOnly dataFinal, CancellationToken cancellationToken) =>
        await dbContext.ContasReceber.AsNoTracking()
            .Where(c => c.StatusContaId != StatusConta.LiquidadaId &&
                        c.StatusContaId != StatusConta.CanceladaId &&
                        c.DataVencimento <= dataFinal)
            .SumAsync(c => (decimal?)c.ValorLiquido, cancellationToken) ?? 0m;

    private async Task<IReadOnlyList<DashboardContaResumoResponse>> CarregarContasVencidasAsync(
        DateOnly dataReferencia, IReadOnlyDictionary<Guid, string> pessoas, CancellationToken cancellationToken)
    {
        var contasPagar = await dbContext.ContasPagar.AsNoTracking()
            .Where(c => c.StatusContaId != StatusConta.LiquidadaId &&
                        c.StatusContaId != StatusConta.CanceladaId &&
                        c.StatusContaId != StatusConta.EmFaturaId &&
                        c.DataVencimento < dataReferencia)
            .OrderBy(c => c.DataVencimento).Take(5)
            .Select(c => new { c.Id, c.Descricao, PessoaId = c.RecebedorId, c.DataVencimento, c.ValorLiquido })
            .ToListAsync(cancellationToken);

        var contasReceber = await dbContext.ContasReceber.AsNoTracking()
            .Where(c => c.StatusContaId != StatusConta.LiquidadaId &&
                        c.StatusContaId != StatusConta.CanceladaId &&
                        c.DataVencimento < dataReferencia)
            .OrderBy(c => c.DataVencimento).Take(5)
            .Select(c => new { c.Id, c.Descricao, PessoaId = c.PagadorId, c.DataVencimento, c.ValorLiquido })
            .ToListAsync(cancellationToken);

        return contasPagar
            .Select(c => new DashboardContaResumoResponse(c.Id, "ContaPagar", c.Descricao, pessoas.GetValueOrDefault(c.PessoaId, string.Empty), c.DataVencimento, c.ValorLiquido, "VENCIDA", "Vencida"))
            .Concat(contasReceber.Select(c => new DashboardContaResumoResponse(c.Id, "ContaReceber", c.Descricao, pessoas.GetValueOrDefault(c.PessoaId, string.Empty), c.DataVencimento, c.ValorLiquido, "VENCIDA", "Vencida")))
            .OrderBy(c => c.DataVencimento).Take(5).ToList();
    }

    private async Task<IReadOnlyList<DashboardContaResumoResponse>> CarregarContasAVencerAsync(
        DateOnly dataInicial, DateOnly dataFinal, IReadOnlyDictionary<Guid, string> pessoas, CancellationToken cancellationToken)
    {
        var contasPagar = await dbContext.ContasPagar.AsNoTracking()
            .Where(c => c.StatusContaId != StatusConta.LiquidadaId &&
                        c.StatusContaId != StatusConta.CanceladaId &&
                        c.StatusContaId != StatusConta.EmFaturaId &&
                        c.DataVencimento >= dataInicial && c.DataVencimento <= dataFinal)
            .OrderBy(c => c.DataVencimento).Take(10)
            .Select(c => new { c.Id, c.Descricao, PessoaId = c.RecebedorId, c.DataVencimento, c.ValorLiquido, c.StatusContaId })
            .ToListAsync(cancellationToken);

        var contasReceber = await dbContext.ContasReceber.AsNoTracking()
            .Where(c => c.StatusContaId != StatusConta.LiquidadaId &&
                        c.StatusContaId != StatusConta.CanceladaId &&
                        c.DataVencimento >= dataInicial && c.DataVencimento <= dataFinal)
            .OrderBy(c => c.DataVencimento).Take(10)
            .Select(c => new { c.Id, c.Descricao, PessoaId = c.PagadorId, c.DataVencimento, c.ValorLiquido, c.StatusContaId })
            .ToListAsync(cancellationToken);

        return contasPagar
            .Select(c => MapearContaResumo(c.Id, "ContaPagar", c.Descricao, c.PessoaId, c.DataVencimento, c.ValorLiquido, c.StatusContaId, pessoas))
            .Concat(contasReceber.Select(c => MapearContaResumo(c.Id, "ContaReceber", c.Descricao, c.PessoaId, c.DataVencimento, c.ValorLiquido, c.StatusContaId, pessoas)))
            .OrderBy(c => c.DataVencimento).Take(10).ToList();
    }

    private async Task<IReadOnlyList<DashboardMovimentacaoResumoResponse>> CarregarMovimentacoesRecentesAsync(
        DateOnly dataReferencia, CancellationToken cancellationToken)
    {
        var movimentacoes = await dbContext.MovimentacoesFinanceiras.AsNoTracking()
            .Where(m => m.Natureza == NaturezaMovimentacao.Realizada &&
                        m.StatusMovimentacaoId != StatusMovimentacao.CanceladaId &&
                        m.DataMovimentacao <= dataReferencia)
            .OrderByDescending(m => m.DataMovimentacao).ThenByDescending(m => m.CreatedAtUtc).Take(10)
            .ToListAsync(cancellationToken);

        return movimentacoes.Select(m => new DashboardMovimentacaoResumoResponse(
            m.Id, m.DataMovimentacao,
            DashboardHelpers.MapearTipoMovimentacao(m.Tipo),
            DashboardHelpers.MapearNaturezaMovimentacao(m.Natureza),
            m.Valor, DashboardHelpers.TruncarObservacao(m.Observacao),
            m.ContaPagarId, m.ContaReceberId, m.FaturaCartaoId)).ToList();
    }

    private static DashboardContaResumoResponse MapearContaResumo(
        Guid id, string tipo, string descricao, Guid pessoaId,
        DateOnly dataVencimento, decimal valor, Guid statusId,
        IReadOnlyDictionary<Guid, string> pessoas)
    {
        var status = DashboardHelpers.StatusContasLookup.GetValueOrDefault(statusId, new StatusContaInfo(string.Empty, string.Empty));
        return new DashboardContaResumoResponse(id, tipo, descricao, pessoas.GetValueOrDefault(pessoaId, string.Empty), dataVencimento, valor, status.Codigo, status.Nome);
    }
}
