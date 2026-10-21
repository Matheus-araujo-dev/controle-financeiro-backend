using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class BuscarFechamentosMensaisTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "buscar_fechamentos_mensais";
    public string Description => "Lista fechamentos mensais com snapshots de receitas, despesas, saldo, pendentes e vencidos. Útil para verificar meses já fechados.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "competencia": { "type": "string", "description": "Competência no formato yyyy-MM (ex: 2026-10). Se omitida, lista os últimos 6 meses." },
            "limite":      { "type": "integer", "description": "Quantidade máxima de registros (padrão 6)" }
          }
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        var input = JsonNode.Parse(inputJson);
        var competenciaStr = input?["competencia"]?.GetValue<string>();
        var limite = input?["limite"]?.GetValue<int>() ?? 6;
        if (limite > 24) limite = 24;

        var query = db.FechamentosMensais.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(competenciaStr))
            query = query.Where(f => f.Competencia == competenciaStr.Trim());

        var fechamentos = await query
            .OrderByDescending(f => f.Competencia)
            .Take(limite)
            .ToListAsync(cancellationToken);

        if (fechamentos.Count == 0)
            return JsonSerializer.Serialize(new { mensagem = "Nenhum fechamento mensal encontrado.", fechamentos = Array.Empty<object>() });

        var resultado = fechamentos.Select(f => new
        {
            id = f.Id,
            competencia = f.Competencia,
            status = f.Status == StatusFechamentoMensal.Fechado ? "Fechado" : "Reaberto",
            totalReceitas = f.TotalReceitasSnapshot,
            totalDespesas = f.TotalDespesasSnapshot,
            saldo = f.SaldoSnapshot,
            totalPendente = f.TotalPendenteSnapshot,
            totalVencido = f.TotalVencidoSnapshot,
            quantidadeLancamentos = f.QuantidadeLancamentosSnapshot,
            fechadoEm = f.FechadoEmUtc.ToString("dd/MM/yyyy HH:mm"),
            justificativaReabertura = f.JustificativaReabertura
        }).ToList();

        return JsonSerializer.Serialize(new
        {
            total = resultado.Count,
            fechamentos = resultado
        });
    }
}
