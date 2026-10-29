using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace ControleFinanceiro.Application.Conciliacao;

internal static class ContasDoCiclo
{
    internal static async Task<IQueryable<ContaPagar>> ConsultarAsync(IAppDbContext db, FaturaCartao fatura, CancellationToken ct)
    {
        var cartao = await db.Cartoes.AsNoTracking().SingleAsync(x => x.Id == fatura.CartaoId, ct);
        var inicio = DateOnly.ParseExact(fatura.Competencia + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (cartao.DiaVencimentoFatura <= cartao.DiaFechamentoFatura) inicio = inicio.AddMonths(1);
        var fim = inicio.AddMonths(1);
        return db.ContasPagar.Where(x => x.CartaoId == fatura.CartaoId
            && x.StatusContaId != StatusConta.CanceladaId
            && (x.FaturaCartaoId == fatura.Id || (!x.FaturaCartaoId.HasValue && x.DataVencimento >= inicio && x.DataVencimento < fim)));
    }
}
