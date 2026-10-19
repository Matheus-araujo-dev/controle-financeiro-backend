using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Conciliacao;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class IgnorarItemConciliacaoTool(ConciliacaoFaturaAppService conciliacao) : IFinanceTool
{
    public string Name => "ignorar_item_conciliacao";
    public string Description => "Ignora um item da fatura na conciliação (marca como não relevante). Use quando o usuário confirmar que não deseja lançar aquele item.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "faturaId": { "type": "string", "description": "ID da fatura (GUID)" },
            "conciliacaoId": { "type": "string", "description": "ID da sessão de conciliação (GUID)" },
            "itemId": { "type": "string", "description": "ID do item pendente (GUID)" }
          },
          "required": ["faturaId", "conciliacaoId", "itemId"]
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        var input = JsonNode.Parse(inputJson);

        if (!Guid.TryParse(input?["faturaId"]?.GetValue<string>(), out var faturaId))
            return JsonSerializer.Serialize(new { erro = "faturaId inválido." });
        if (!Guid.TryParse(input?["conciliacaoId"]?.GetValue<string>(), out var conciliacaoId))
            return JsonSerializer.Serialize(new { erro = "conciliacaoId inválido." });
        if (!Guid.TryParse(input?["itemId"]?.GetValue<string>(), out var itemId))
            return JsonSerializer.Serialize(new { erro = "itemId inválido." });

        try
        {
            var ok = await conciliacao.IgnorarAsync(faturaId, conciliacaoId, itemId, cancellationToken);
            if (!ok)
                return JsonSerializer.Serialize(new { erro = "Item ou sessão de conciliação não encontrados." });

            return JsonSerializer.Serialize(new
            {
                sucesso = true,
                mensagem = "Item ignorado com sucesso.",
                itemId
            });
        }
        catch (Exception ex) when (ex.Message.Contains("item") || ex.Message.Contains("conciliado") || ex.Message.Contains("Conciliacao"))
        {
            return JsonSerializer.Serialize(new { erro = ex.Message });
        }
    }
}
