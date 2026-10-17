using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class CompararMesesTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "comparar_meses";
    public string Description => "Compara dois meses lado a lado: total de despesas, receitas, saldo e maiores categorias. Útil para 'comparar outubro com setembro', 'evolução mês a mês', 'gastei mais ou menos?'.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "mes1": { "type": "integer", "description": "Primeiro mês (1-12)" },
            "ano1": { "type": "integer", "description": "Ano do primeiro mês" },
            "mes2": { "type": "integer", "description": "Segundo mês (1-12)" },
            "ano2": { "type": "integer", "description": "Ano do segundo mês" }
          },
          "required": ["mes1", "ano1", "mes2", "ano2"]
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        var input = JsonNode.Parse(inputJson);
        var mes1 = input?["mes1"]?.GetValue<int>() ?? 1;
        var ano1 = input?["ano1"]?.GetValue<int>() ?? DateTime.UtcNow.Year;
        var mes2 = input?["mes2"]?.GetValue<int>() ?? 1;
        var ano2 = input?["ano2"]?.GetValue<int>() ?? DateTime.UtcNow.Year;

        var resumo1 = await CalcularResumoAsync(mes1, ano1, cancellationToken);
        var resumo2 = await CalcularResumoAsync(mes2, ano2, cancellationToken);

        var diffDespesas = resumo2.TotalDespesas - resumo1.TotalDespesas;
        var diffReceitas = resumo2.TotalReceitas - resumo1.TotalReceitas;

        return JsonSerializer.Serialize(new
        {
            periodo1 = new
            {
                periodo = $"{mes1:D2}/{ano1}",
                totalDespesas = resumo1.TotalDespesas,
                totalReceitas = resumo1.TotalReceitas,
                saldo = resumo1.TotalReceitas - resumo1.TotalDespesas,
                maioresCategorias = resumo1.TopCategorias
            },
            periodo2 = new
            {
                periodo = $"{mes2:D2}/{ano2}",
                totalDespesas = resumo2.TotalDespesas,
                totalReceitas = resumo2.TotalReceitas,
                saldo = resumo2.TotalReceitas - resumo2.TotalDespesas,
                maioresCategorias = resumo2.TopCategorias
            },
            variacao = new
            {
                despesas = diffDespesas,
                despesasPercent = resumo1.TotalDespesas != 0
                    ? Math.Round((double)diffDespesas / (double)resumo1.TotalDespesas * 100, 1) : 0,
                receitas = diffReceitas,
                receitasPercent = resumo1.TotalReceitas != 0
                    ? Math.Round((double)diffReceitas / (double)resumo1.TotalReceitas * 100, 1) : 0
            }
        });
    }

    private async Task<ResumoMes> CalcularResumoAsync(int mes, int ano, CancellationToken ct)
    {
        var inicio = new DateOnly(ano, mes, 1);
        var fim = inicio.AddMonths(1).AddDays(-1);

        var despesas = await db.ContasPagar
            .AsNoTracking()
            .Where(c => c.DataVencimento >= inicio && c.DataVencimento <= fim
                     && c.StatusContaId != StatusConta.CanceladaId)
            .SumAsync(c => c.ValorLiquido, ct);

        var receitas = await db.ContasReceber
            .AsNoTracking()
            .Where(c => c.DataVencimento >= inicio && c.DataVencimento <= fim
                     && c.StatusContaId != StatusConta.CanceladaId)
            .SumAsync(c => c.ValorLiquido, ct);

        var topCategorias = await (
            from r in db.RateiosContaGerencial.AsNoTracking()
            join cp in db.ContasPagar.AsNoTracking() on r.ContaPagarId equals cp.Id
            join cg in db.ContasGerenciais.AsNoTracking() on r.ContaGerencialId equals cg.Id
            where r.ContaPagarId.HasValue
               && cp.DataVencimento >= inicio && cp.DataVencimento <= fim
               && cp.StatusContaId != StatusConta.CanceladaId
            group r by new { cg.Id, cg.Descricao } into g
            orderby g.Sum(r => r.Valor) descending
            select new { categoria = g.Key.Descricao, total = g.Sum(r => r.Valor) }
        ).Take(5).ToListAsync(ct);

        return new ResumoMes(despesas, receitas, topCategorias.Select(c => new CategoriaResumo(c.categoria, c.total)).ToArray());
    }

    private sealed record ResumoMes(decimal TotalDespesas, decimal TotalReceitas, CategoriaResumo[] TopCategorias);
    private sealed record CategoriaResumo(string Categoria, decimal Total);
}
