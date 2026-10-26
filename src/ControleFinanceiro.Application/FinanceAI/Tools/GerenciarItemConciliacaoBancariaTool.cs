using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Conciliacao;
using ControleFinanceiro.Contracts.Conciliacao;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class GerenciarItemConciliacaoBancariaTool(ConciliacaoAppService conciliacao) : IFinanceTool
{
    public string Name => "gerenciar_item_conciliacao_bancaria";
    public string Description => "Concilia ou ignora um item de conciliação bancária. Para conciliar, vincule a uma movimentação existente (use a sugestão automática quando disponível).";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "acao":            { "type": "string",  "enum": ["conciliar", "ignorar"], "description": "Ação a executar" },
            "conciliacaoId":   { "type": "string",  "description": "ID da conciliação (GUID)" },
            "itemId":          { "type": "string",  "description": "ID do item a conciliar/ignorar (GUID)" },
            "movimentacaoId":  { "type": "string",  "description": "ID da movimentação para vincular (GUID, opcional para conciliar — se omitido, marca como conciliado sem vínculo)" }
          },
          "required": ["acao", "conciliacaoId", "itemId"]
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        var input = JsonNode.Parse(inputJson);
        var acao = input?["acao"]?.GetValue<string>();

        if (!Guid.TryParse(input?["conciliacaoId"]?.GetValue<string>(), out var conciliacaoId))
            return JsonSerializer.Serialize(new { erro = "conciliacaoId é obrigatório e deve ser um GUID válido." });

        if (!Guid.TryParse(input?["itemId"]?.GetValue<string>(), out var itemId))
            return JsonSerializer.Serialize(new { erro = "itemId é obrigatório e deve ser um GUID válido." });

        try
        {
            return acao switch
            {
                "conciliar" => await ConciliarAsync(input!, conciliacaoId, itemId, cancellationToken),
                "ignorar" => await IgnorarAsync(conciliacaoId, itemId, cancellationToken),
                _ => JsonSerializer.Serialize(new { erro = "Ação inválida. Use: conciliar ou ignorar." })
            };
        }
        catch (InvalidOperationException ex)
        {
            return JsonSerializer.Serialize(new { erro = ex.Message });
        }
    }

    private async Task<string> ConciliarAsync(JsonNode input, Guid conciliacaoId, Guid itemId, CancellationToken ct)
    {
        Guid? movimentacaoId = null;
        if (Guid.TryParse(input["movimentacaoId"]?.GetValue<string>(), out var mid))
            movimentacaoId = mid;

        var request = new ConciliarItemRequest(movimentacaoId);
        var ok = await conciliacao.ConciliarItemAsync(conciliacaoId, itemId, request, ct);

        if (!ok)
            return JsonSerializer.Serialize(new { erro = "Conciliação ou item não encontrado." });

        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            mensagem = movimentacaoId.HasValue
                ? "Item conciliado e vinculado à movimentação."
                : "Item marcado como conciliado (sem vínculo a movimentação)."
        });
    }

    private async Task<string> IgnorarAsync(Guid conciliacaoId, Guid itemId, CancellationToken ct)
    {
        var ok = await conciliacao.IgnorarItemAsync(conciliacaoId, itemId, ct);

        if (!ok)
            return JsonSerializer.Serialize(new { erro = "Conciliação ou item não encontrado." });

        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            mensagem = "Item ignorado na conciliação."
        });
    }
}
