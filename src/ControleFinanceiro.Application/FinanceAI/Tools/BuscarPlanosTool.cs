using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class BuscarPlanosTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "buscar_planos";
    public string Description => "Lista os planos de acumulação/poupança da família. Mostra valor mensal, parcelas pagas, total acumulado e progresso.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "apenasAtivos": { "type": "boolean", "description": "Se true, retorna apenas planos ativos (padrão true)" }
          }
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        var input = JsonNode.Parse(inputJson);
        var apenasAtivos = input?["apenasAtivos"]?.GetValue<bool>() ?? true;

        var query = db.Planos.AsNoTracking().AsQueryable();

        if (apenasAtivos)
            query = query.Where(p => !p.Cancelado && p.ParcelasPagas < p.NumParcelas);

        var contasDict = await db.ContasBancarias
            .AsNoTracking()
            .Select(c => new { c.Id, c.Nome })
            .ToDictionaryAsync(c => c.Id, c => c.Nome, cancellationToken);

        var planos = await query
            .OrderBy(p => p.Nome)
            .ToListAsync(cancellationToken);

        var resultado = planos.Select(p => new
        {
            id = p.Id,
            nome = p.Nome,
            descricao = p.Descricao,
            valorMensal = p.ValorMensal,
            numParcelas = p.NumParcelas,
            parcelasPagas = p.ParcelasPagas,
            totalAcumulado = p.TotalAcumulado,
            totalRetirado = p.TotalRetirado,
            valorTotal = p.ValorTotal,
            contaBancariaCaixa = contasDict.GetValueOrDefault(p.ContaBancariaCaixaId, "Desconhecida"),
            status = p.Cancelado ? "Cancelado" : p.Concluido ? "Concluído" : "Em andamento",
            progressoPercent = p.NumParcelas > 0
                ? decimal.Round((decimal)p.ParcelasPagas / p.NumParcelas * 100, 1)
                : 0m
        }).ToList();

        return JsonSerializer.Serialize(new
        {
            total = resultado.Count,
            totalAcumuladoGeral = planos.Sum(p => p.TotalAcumulado),
            planos = resultado
        });
    }
}
