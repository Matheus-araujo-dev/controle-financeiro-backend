using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Financeiro;
using ControleFinanceiro.Domain.Identidade;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class CriarTransferenciaTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "criar_transferencia";
    public string Description => "Cria uma transferência entre contas bancárias.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "contaOrigemId":   { "type": "string", "description": "ID da conta bancária de origem" },
            "contaDestinoId":  { "type": "string", "description": "ID da conta bancária de destino" },
            "valor":           { "type": "number", "description": "Valor da transferência em reais" },
            "data":            { "type": "string", "description": "Data no formato YYYY-MM-DD. Se omitida, usa hoje." },
            "descricao":       { "type": "string", "description": "Descrição da transferência (opcional)" }
          },
          "required": ["contaOrigemId", "contaDestinoId", "valor"]
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        if (context.Papel == PapelFamilia.Visualizador)
            return JsonSerializer.Serialize(new { erro = "Visualizadores não podem criar transferências." });

        var input = JsonNode.Parse(inputJson);
        var contaOrigemIdStr = input?["contaOrigemId"]?.GetValue<string>();
        var contaDestinoIdStr = input?["contaDestinoId"]?.GetValue<string>();
        var valor = input?["valor"]?.GetValue<decimal>() ?? 0;
        var dataStr = input?["data"]?.GetValue<string>();
        var descricao = input?["descricao"]?.GetValue<string>();

        if (!Guid.TryParse(contaOrigemIdStr, out var contaOrigemId))
            return JsonSerializer.Serialize(new { erro = "contaOrigemId inválido." });

        if (!Guid.TryParse(contaDestinoIdStr, out var contaDestinoId))
            return JsonSerializer.Serialize(new { erro = "contaDestinoId inválido." });

        if (valor <= 0)
            return JsonSerializer.Serialize(new { erro = "Valor deve ser maior que zero." });

        var contasExistentes = await db.ContasBancarias
            .AsNoTracking()
            .Where(c => c.Id == contaOrigemId || c.Id == contaDestinoId)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        if (!contasExistentes.Contains(contaOrigemId))
            return JsonSerializer.Serialize(new { erro = "Conta bancária de origem não encontrada." });

        if (!contasExistentes.Contains(contaDestinoId))
            return JsonSerializer.Serialize(new { erro = "Conta bancária de destino não encontrada." });

        var data = DateOnly.TryParse(dataStr, out var d) ? d : DateOnly.FromDateTime(DateTime.UtcNow);

        try
        {
            var transferencia = Transferencia.Criar(contaOrigemId, contaDestinoId, valor, data, descricao);
            db.Transferencias.Add(transferencia);
            await db.SaveChangesAsync(cancellationToken);

            return JsonSerializer.Serialize(new
            {
                sucesso = true,
                transferenciaId = transferencia.Id,
                mensagem = $"Transferência de R$ {valor:N2} criada em {data:dd/MM/yyyy}."
            });
        }
        catch (ArgumentException ex)
        {
            return JsonSerializer.Serialize(new { erro = ex.Message });
        }
    }
}
