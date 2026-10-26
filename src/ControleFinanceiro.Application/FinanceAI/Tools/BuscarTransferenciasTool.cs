using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class BuscarTransferenciasTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "buscar_transferencias";
    public string Description => "Lista transferências entre contas bancárias. Pode filtrar por período, conta específica ou incluir canceladas.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "dataInicio":         { "type": "string", "description": "Data inicial YYYY-MM-DD (opcional)" },
            "dataFim":            { "type": "string", "description": "Data final YYYY-MM-DD (opcional)" },
            "contaBancariaId":    { "type": "string", "description": "Filtra por conta (origem ou destino). Opcional." },
            "incluirCanceladas":  { "type": "boolean", "description": "Incluir canceladas (padrão false)" }
          }
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        var input = JsonNode.Parse(inputJson);
        var dataInicioStr = input?["dataInicio"]?.GetValue<string>();
        var dataFimStr = input?["dataFim"]?.GetValue<string>();
        var contaBancariaIdStr = input?["contaBancariaId"]?.GetValue<string>();
        var incluirCanceladas = input?["incluirCanceladas"]?.GetValue<bool>() ?? false;

        var query = db.Transferencias.AsNoTracking().AsQueryable();

        if (!incluirCanceladas)
            query = query.Where(t => !t.Cancelada);

        if (DateOnly.TryParse(dataInicioStr, out var dataInicio))
            query = query.Where(t => t.DataTransferencia >= dataInicio);

        if (DateOnly.TryParse(dataFimStr, out var dataFim))
            query = query.Where(t => t.DataTransferencia <= dataFim);

        if (Guid.TryParse(contaBancariaIdStr, out var contaBancariaId))
            query = query.Where(t => t.ContaBancariaOrigemId == contaBancariaId || t.ContaBancariaDestinoId == contaBancariaId);

        var contasDict = await db.ContasBancarias
            .AsNoTracking()
            .Select(c => new { c.Id, c.Nome })
            .ToDictionaryAsync(c => c.Id, c => c.Nome, cancellationToken);

        var transferencias = await query
            .OrderByDescending(t => t.DataTransferencia)
            .Take(50)
            .ToListAsync(cancellationToken);

        var resultado = transferencias.Select(t => new
        {
            id = t.Id,
            contaOrigem = contasDict.GetValueOrDefault(t.ContaBancariaOrigemId, "Desconhecida"),
            contaDestino = contasDict.GetValueOrDefault(t.ContaBancariaDestinoId, "Desconhecida"),
            valor = t.Valor,
            dataTransferencia = t.DataTransferencia.ToString("dd/MM/yyyy"),
            descricao = t.Descricao,
            cancelada = t.Cancelada
        }).ToList();

        return JsonSerializer.Serialize(new
        {
            total = resultado.Count,
            totalTransferido = resultado.Where(r => !r.cancelada).Sum(r => r.valor),
            transferencias = resultado
        });
    }
}
