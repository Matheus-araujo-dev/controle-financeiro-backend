using System.Globalization;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Contracts.Dashboard;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Dashboard;

public interface IDashboardComparativoMensalService
{
    Task<DashboardComparativoMensalResponse> ObterComparativoMensalAsync(DashboardComparativoMensalQueryRequest query, CancellationToken cancellationToken);
}

public sealed class DashboardComparativoMensalService(IAppDbContext dbContext) : IDashboardComparativoMensalService
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public async Task<DashboardComparativoMensalResponse> ObterComparativoMensalAsync(
        DashboardComparativoMensalQueryRequest query,
        CancellationToken cancellationToken)
    {
        var meses = Math.Clamp(query.Meses, 2, 12);
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var inicioRange = new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(-(meses - 1));
        var fimRange = new DateOnly(hoje.Year, hoje.Month, DateTime.DaysInMonth(hoje.Year, hoje.Month));

        var receitasPorMes = await dbContext.ContasReceber
            .AsNoTracking()
            .Where(c => c.StatusContaId != StatusConta.CanceladaId &&
                        c.DataEmissao >= inicioRange &&
                        c.DataEmissao <= fimRange)
            .ToListAsync(cancellationToken);

        var despesasPorMes = await dbContext.ContasPagar
            .AsNoTracking()
            .Where(c => c.StatusContaId != StatusConta.CanceladaId &&
                        c.DataEmissao >= inicioRange &&
                        c.DataEmissao <= fimRange)
            .ToListAsync(cancellationToken);

        var receitasMap = receitasPorMes
            .GroupBy(c => new DateOnly(c.DataEmissao.Year, c.DataEmissao.Month, 1))
            .ToDictionary(g => g.Key, g => g.Sum(c => c.ValorLiquido));

        var despesasMap = despesasPorMes
            .GroupBy(c => new DateOnly(c.DataEmissao.Year, c.DataEmissao.Month, 1))
            .ToDictionary(g => g.Key, g => g.Sum(c => c.ValorLiquido));

        var itens = new List<DashboardComparativoMensalItemResponse>(meses);

        for (var i = meses - 1; i >= 0; i--)
        {
            var data = new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(-i);
            var chave = data;

            var r = decimal.Round(receitasMap.GetValueOrDefault(chave, 0m), 2, MidpointRounding.AwayFromZero);
            var d = decimal.Round(despesasMap.GetValueOrDefault(chave, 0m), 2, MidpointRounding.AwayFromZero);

            var competencia = $"{data.Year:D4}-{data.Month:D2}";
            var nomesMes = PtBr.DateTimeFormat.GetMonthName(data.Month);
            var label = $"{char.ToUpperInvariant(nomesMes[0])}{nomesMes[1..3]}/{data.Year % 100:D2}";

            itens.Add(new DashboardComparativoMensalItemResponse(competencia, label, r, d, decimal.Round(r - d, 2, MidpointRounding.AwayFromZero), null, null));
        }

        for (var j = 1; j < itens.Count; j++)
        {
            var prev = itens[j - 1];
            var curr = itens[j];

            var varR = prev.Receitas != 0
                ? decimal.Round((curr.Receitas - prev.Receitas) / prev.Receitas * 100, 1, MidpointRounding.AwayFromZero)
                : (decimal?)null;

            var varD = prev.Despesas != 0
                ? decimal.Round((curr.Despesas - prev.Despesas) / prev.Despesas * 100, 1, MidpointRounding.AwayFromZero)
                : (decimal?)null;

            itens[j] = curr with { VariacaoReceitas = varR, VariacaoDespesas = varD };
        }

        return new DashboardComparativoMensalResponse(itens);
    }
}
