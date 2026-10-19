using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Identidade;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class GerenciarFechamentoTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "gerenciar_fechamento";
    public string Description => "Gerencia fechamentos mensais: reabrir um mês fechado (requer justificativa). Para fechar um mês, use o aplicativo — o fechamento envolve cálculos complexos de snapshots. Para consultar, use buscar_fechamentos_mensais.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "acao":           { "type": "string",  "enum": ["reabrir"], "description": "Ação a executar (apenas reabrir)" },
            "competencia":    { "type": "string",  "description": "Competência no formato YYYY-MM" },
            "justificativa":  { "type": "string",  "description": "Justificativa para reabertura (obrigatória)" }
          },
          "required": ["acao", "competencia", "justificativa"]
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        if (context.Papel != PapelFamilia.Administrador)
            return JsonSerializer.Serialize(new { erro = "Apenas administradores podem gerenciar fechamentos." });

        var input = JsonNode.Parse(inputJson);
        var acao = input?["acao"]?.GetValue<string>();
        var competencia = input?["competencia"]?.GetValue<string>();
        var justificativa = input?["justificativa"]?.GetValue<string>();

        if (acao != "reabrir")
            return JsonSerializer.Serialize(new { erro = "Apenas a ação 'reabrir' é suportada via agente. Para fechar um mês, use o aplicativo." });

        if (string.IsNullOrWhiteSpace(competencia))
            return JsonSerializer.Serialize(new { erro = "Competência é obrigatória (formato YYYY-MM)." });

        if (string.IsNullOrWhiteSpace(justificativa))
            return JsonSerializer.Serialize(new { erro = "Justificativa é obrigatória para reabrir um mês." });

        try
        {
            var fechamento = await db.FechamentosMensais
                .FirstOrDefaultAsync(f => f.Competencia == competencia, cancellationToken);

            if (fechamento is null)
                return JsonSerializer.Serialize(new { erro = $"Fechamento para a competência {competencia} não encontrado." });

            fechamento.Reabrir(context.UsuarioId, DateTimeOffset.UtcNow, justificativa);
            await db.SaveChangesAsync(cancellationToken);

            return JsonSerializer.Serialize(new
            {
                sucesso = true,
                mensagem = $"Mês {competencia} reaberto com sucesso. Justificativa: {justificativa}"
            });
        }
        catch (InvalidOperationException ex)
        {
            return JsonSerializer.Serialize(new { erro = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return JsonSerializer.Serialize(new { erro = ex.Message });
        }
    }
}
