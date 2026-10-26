using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class BuscarMetasOrcamentoTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "buscar_metas_orcamento";
    public string Description => "Lista metas de orçamento por categoria e competência, comparando o valor da meta com o gasto real (contas a pagar liquidadas ou pendentes no período).";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "competencia": { "type": "string", "description": "Competência no formato yyyy-MM (ex: 2026-10). Se omitida, usa o mês atual." }
          }
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        var input = JsonNode.Parse(inputJson);
        var competenciaStr = input?["competencia"]?.GetValue<string>();

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var competencia = string.IsNullOrWhiteSpace(competenciaStr)
            ? $"{hoje.Year:D4}-{hoje.Month:D2}"
            : competenciaStr.Trim();

        var metas = await db.MetasOrcamento
            .AsNoTracking()
            .Where(m => m.Competencia == competencia)
            .ToListAsync(cancellationToken);

        if (metas.Count == 0)
            return JsonSerializer.Serialize(new { competencia, mensagem = "Nenhuma meta cadastrada para esta competência.", metas = Array.Empty<object>() });

        var categoriaIds = metas.Select(m => m.ContaGerencialId).Distinct().ToList();

        var categoriasDict = await db.ContasGerenciais
            .AsNoTracking()
            .Where(c => categoriaIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Descricao })
            .ToDictionaryAsync(c => c.Id, c => c.Descricao, cancellationToken);

        if (!DateOnly.TryParseExact($"{competencia}-01", "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var inicioMes))
        {
            return JsonSerializer.Serialize(new { erro = "Competência inválida. Use o formato yyyy-MM." });
        }

        var fimMes = inicioMes.AddMonths(1).AddDays(-1);

        var gastosReais = await db.ContasPagar
            .AsNoTracking()
            .Where(cp => cp.DataVencimento >= inicioMes && cp.DataVencimento <= fimMes
                      && cp.StatusContaId != StatusConta.CanceladaId)
            .Join(db.RateiosContaGerencial.AsNoTracking(),
                cp => cp.Id,
                r => r.ContaPagarId,
                (cp, r) => new { r.ContaGerencialId, r.Valor })
            .Where(x => categoriaIds.Contains(x.ContaGerencialId))
            .GroupBy(x => x.ContaGerencialId)
            .Select(g => new { ContaGerencialId = g.Key, TotalGasto = g.Sum(x => x.Valor) })
            .ToDictionaryAsync(x => x.ContaGerencialId, x => x.TotalGasto, cancellationToken);

        var resultado = metas.Select(m =>
        {
            var gasto = gastosReais.GetValueOrDefault(m.ContaGerencialId, 0m);
            var percentual = m.ValorMeta > 0 ? Math.Round((double)(gasto / m.ValorMeta * 100m), 1) : 0;
            return new
            {
                id = m.Id,
                categoria = categoriasDict.GetValueOrDefault(m.ContaGerencialId, "Desconhecida"),
                contaGerencialId = m.ContaGerencialId,
                valorMeta = m.ValorMeta,
                gastoReal = gasto,
                saldoRestante = m.ValorMeta - gasto,
                percentualUtilizado = percentual,
                estourou = gasto > m.ValorMeta
            };
        })
        .OrderByDescending(x => x.percentualUtilizado)
        .ToList();

        return JsonSerializer.Serialize(new
        {
            competencia,
            totalMetas = resultado.Count,
            totalOrcado = resultado.Sum(x => x.valorMeta),
            totalGasto = resultado.Sum(x => x.gastoReal),
            metas = resultado
        });
    }
}
