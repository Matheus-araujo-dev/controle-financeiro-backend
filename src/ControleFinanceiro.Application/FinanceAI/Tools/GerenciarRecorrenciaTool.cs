using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Identidade;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class GerenciarRecorrenciaTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "gerenciar_recorrencia";
    public string Description => "Gerencia regras de recorrência: pausar, retomar ou encerrar uma recorrência existente. Apenas administradores.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "recorrenciaId": { "type": "string", "description": "ID da regra de recorrência" },
            "acao":          { "type": "string", "enum": ["pausar", "retomar", "encerrar"], "description": "Ação a executar" },
            "dataFim":       { "type": "string", "description": "Data fim no formato YYYY-MM-DD. Obrigatória para ação 'encerrar'." }
          },
          "required": ["recorrenciaId", "acao"]
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        if (context.Papel != PapelFamilia.Administrador)
            return JsonSerializer.Serialize(new { erro = "Apenas administradores podem gerenciar recorrências." });

        var input = JsonNode.Parse(inputJson);
        var recorrenciaIdStr = input?["recorrenciaId"]?.GetValue<string>();
        var acao = input?["acao"]?.GetValue<string>();
        var dataFimStr = input?["dataFim"]?.GetValue<string>();

        if (!Guid.TryParse(recorrenciaIdStr, out var recorrenciaId))
            return JsonSerializer.Serialize(new { erro = "recorrenciaId inválido." });

        if (acao != "pausar" && acao != "retomar" && acao != "encerrar")
            return JsonSerializer.Serialize(new { erro = "Ação deve ser 'pausar', 'retomar' ou 'encerrar'." });

        if (acao == "encerrar" && !DateOnly.TryParse(dataFimStr, out _))
            return JsonSerializer.Serialize(new { erro = "dataFim é obrigatória para encerrar uma recorrência (formato YYYY-MM-DD)." });

        var regra = await db.RegrasRecorrencia
            .FirstOrDefaultAsync(r => r.Id == recorrenciaId, cancellationToken);

        if (regra is null)
            return JsonSerializer.Serialize(new { erro = "Regra de recorrência não encontrada." });

        try
        {
            var mensagem = acao switch
            {
                "pausar" => Pausar(regra),
                "retomar" => Retomar(regra),
                "encerrar" => Encerrar(regra, DateOnly.Parse(dataFimStr!)),
                _ => throw new InvalidOperationException("Ação inválida.")
            };

            await db.SaveChangesAsync(cancellationToken);

            return JsonSerializer.Serialize(new { sucesso = true, mensagem });
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

    private static string Pausar(Domain.Financeiro.RegraRecorrencia regra)
    {
        regra.Pausar();
        return "Recorrência pausada com sucesso.";
    }

    private static string Retomar(Domain.Financeiro.RegraRecorrencia regra)
    {
        regra.Retomar();
        return "Recorrência retomada com sucesso.";
    }

    private static string Encerrar(Domain.Financeiro.RegraRecorrencia regra, DateOnly dataFim)
    {
        regra.Encerrar(dataFim);
        return $"Recorrência encerrada com data fim {dataFim:dd/MM/yyyy}.";
    }
}
