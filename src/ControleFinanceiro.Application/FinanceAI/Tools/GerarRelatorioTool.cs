using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class GerarRelatorioTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "gerar_relatorio";
    public string Description => "Gera dados consolidados para relatório financeiro mensal completo: despesas e receitas por categoria, por responsável, contas vencidas, evolução do saldo e indicadores. O LLM deve formatar a resposta de forma amigável.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "mes": { "type": "integer", "description": "Mês (1-12)" },
            "ano": { "type": "integer", "description": "Ano (ex: 2026)" }
          },
          "required": ["mes", "ano"]
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        var input = JsonNode.Parse(inputJson);
        var mes = input?["mes"]?.GetValue<int>() ?? DateTime.UtcNow.Month;
        var ano = input?["ano"]?.GetValue<int>() ?? DateTime.UtcNow.Year;

        var inicio = new DateOnly(ano, mes, 1);
        var fim = inicio.AddMonths(1).AddDays(-1);
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);

        var despesasPorCategoria = await (
            from r in db.RateiosContaGerencial.AsNoTracking()
            join cp in db.ContasPagar.AsNoTracking() on r.ContaPagarId equals cp.Id
            join cg in db.ContasGerenciais.AsNoTracking() on r.ContaGerencialId equals cg.Id
            where r.ContaPagarId.HasValue
               && cp.DataVencimento >= inicio && cp.DataVencimento <= fim
               && cp.StatusContaId != StatusConta.CanceladaId
            group r by cg.Descricao into g
            orderby g.Sum(r => r.Valor) descending
            select new { categoria = g.Key, total = g.Sum(r => r.Valor) }
        ).ToListAsync(cancellationToken);

        var receitasPorCategoria = await (
            from r in db.RateiosContaGerencial.AsNoTracking()
            join cr in db.ContasReceber.AsNoTracking() on r.ContaReceberId equals cr.Id
            join cg in db.ContasGerenciais.AsNoTracking() on r.ContaGerencialId equals cg.Id
            where r.ContaReceberId.HasValue
               && cr.DataVencimento >= inicio && cr.DataVencimento <= fim
               && cr.StatusContaId != StatusConta.CanceladaId
            group r by cg.Descricao into g
            orderby g.Sum(r => r.Valor) descending
            select new { categoria = g.Key, total = g.Sum(r => r.Valor) }
        ).ToListAsync(cancellationToken);

        var despesasPorResponsavel = await (
            from cp in db.ContasPagar.AsNoTracking()
            join p in db.Pessoas.AsNoTracking() on cp.RecebedorId equals p.Id
            where cp.DataVencimento >= inicio && cp.DataVencimento <= fim
               && cp.StatusContaId != StatusConta.CanceladaId
            group cp by p.Nome into g
            orderby g.Sum(c => c.ValorLiquido) descending
            select new { responsavel = g.Key, total = g.Sum(c => c.ValorLiquido) }
        ).Take(10).ToListAsync(cancellationToken);

        var totalDespesas = despesasPorCategoria.Sum(d => d.total);
        var totalReceitas = receitasPorCategoria.Sum(r => r.total);

        var contasVencidas = await db.ContasPagar
            .AsNoTracking()
            .Where(c => c.DataVencimento >= inicio && c.DataVencimento < hoje
                     && c.StatusContaId == StatusConta.PendenteId)
            .CountAsync(cancellationToken);

        var valorVencido = await db.ContasPagar
            .AsNoTracking()
            .Where(c => c.DataVencimento >= inicio && c.DataVencimento < hoje
                     && c.StatusContaId == StatusConta.PendenteId)
            .SumAsync(c => c.ValorLiquido, cancellationToken);

        var contasPagas = await db.ContasPagar
            .AsNoTracking()
            .Where(c => c.DataVencimento >= inicio && c.DataVencimento <= fim
                     && c.StatusContaId == StatusConta.LiquidadaId)
            .CountAsync(cancellationToken);

        var contasPendentes = await db.ContasPagar
            .AsNoTracking()
            .Where(c => c.DataVencimento >= inicio && c.DataVencimento <= fim
                     && c.StatusContaId == StatusConta.PendenteId)
            .CountAsync(cancellationToken);

        return JsonSerializer.Serialize(new
        {
            periodo = $"{mes:D2}/{ano}",
            resumo = new
            {
                totalDespesas,
                totalReceitas,
                saldo = totalReceitas - totalDespesas
            },
            despesasPorCategoria,
            receitasPorCategoria,
            despesasPorResponsavel,
            indicadores = new
            {
                contasPagas,
                contasPendentes,
                contasVencidas,
                valorVencido
            }
        });
    }
}
