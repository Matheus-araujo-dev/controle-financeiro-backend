using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Contracts.Dashboard;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Dashboard;

public sealed class DashboardAnomaliasService(IAppDbContext db)
{
    public async Task<DashboardAnomaliasResponse> ObterAsync(string mesReferencia, CancellationToken cancellationToken)
    {
        var mes = DateOnly.ParseExact(mesReferencia, "yyyy-MM");
        var inicio = mes.AddMonths(-3);
        var fim = mes.AddMonths(1);
        // Query filters preservam isolamento por workspace. Itens de cartão são a compra;
        // a obrigação consolidada, parcelas e créditos não entram nesta heurística.
        var contas = await db.ContasPagar.AsNoTracking()
            .Where(c => c.DataEmissao >= inicio && c.DataEmissao < fim
                && c.StatusContaId != StatusConta.CanceladaId && c.ValorLiquido > 0
                && c.QuantidadeParcelas == 1 && c.GrupoParcelamentoId == null
                && (c.FaturaCartaoId == null || c.CartaoId != null))
            .OrderBy(c => c.DataEmissao).ThenBy(c => c.Id)
            .Select(c => new AnomaliaConta(c.Id, c.DataEmissao, c.Descricao, c.RecebedorId,
                c.ResponsavelCompraId, c.CartaoId, c.ContaBancariaId, c.ValorLiquido, c.RegraRecorrenciaId))
            .Take(5001).ToListAsync(cancellationToken);
        var completo = contas.Count <= 5000;
        return new(mesReferencia, inicio, completo, contas.Count,
            completo ? AnomaliasDetector.Detectar(contas, mes) : []);
    }
}
