using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class BuscarGastosPorCategoriaTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "buscar_gastos_por_categoria";
    public string Description => "Retorna gastos agrupados por categoria financeira em um período (mês/ano). Útil para 'quanto gastei em alimentação?' ou 'como estão os gastos por categoria?'.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "mes":  { "type": "integer", "description": "Mês (1-12)" },
            "ano":  { "type": "integer", "description": "Ano (ex: 2026)" },
            "contaGerencialId": { "type": "string", "description": "ID de uma categoria específica (opcional). Se omitido, retorna todas." }
          },
          "required": ["mes", "ano"]
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        var input = JsonNode.Parse(inputJson);
        var mes = input?["mes"]?.GetValue<int>() ?? DateTime.UtcNow.Month;
        var ano = input?["ano"]?.GetValue<int>() ?? DateTime.UtcNow.Year;
        var categoriaIdStr = input?["contaGerencialId"]?.GetValue<string>();
        Guid? categoriaId = Guid.TryParse(categoriaIdStr, out var cid) ? cid : null;

        var inicio = new DateOnly(ano, mes, 1);
        var fim = inicio.AddMonths(1).AddDays(-1);

        var query =
            from r in db.RateiosContaGerencial.AsNoTracking()
            join cp in db.ContasPagar on r.ContaPagarId equals cp.Id
            join cg in db.ContasGerenciais on r.ContaGerencialId equals cg.Id
            where r.ContaPagarId.HasValue
               && cp.DataVencimento >= inicio && cp.DataVencimento <= fim
               && cp.StatusContaId != StatusConta.CanceladaId
            select new { r, cp, cg };

        if (categoriaId.HasValue)
            query = query.Where(x => x.r.ContaGerencialId == categoriaId.Value);

        var gastos = await query
            .GroupBy(x => new { x.cg.Id, x.cg.Descricao })
            .Select(g => new
            {
                categoriaId = g.Key.Id,
                categoria = g.Key.Descricao,
                total = g.Sum(x => x.r.Valor),
                quantidadeLancamentos = g.Count()
            })
            .OrderByDescending(g => g.total)
            .ToListAsync(cancellationToken);

        return JsonSerializer.Serialize(new
        {
            periodo = $"{mes:D2}/{ano}",
            totalGeral = gastos.Sum(g => g.total),
            categorias = gastos
        });
    }
}
