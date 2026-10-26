using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Conciliacao;
using ControleFinanceiro.Contracts.Conciliacao;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class VincularItemConciliacaoTool(ConciliacaoFaturaAppService conciliacao) : IFinanceTool
{
    public string Name => "vincular_item_conciliacao";
    public string Description => "Vincula um item da fatura a um lançamento já existente no sistema (conciliação). Ajusta valor se necessário e aprende preferências do estabelecimento para futuras importações.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "faturaId": { "type": "string", "description": "ID da fatura (GUID)" },
            "conciliacaoId": { "type": "string", "description": "ID da sessão de conciliação (GUID)" },
            "itemId": { "type": "string", "description": "ID do item pendente (GUID)" },
            "contaPagarId": { "type": "string", "description": "ID do lançamento existente para vincular (GUID)" },
            "valorEsperadoSistema": { "type": "number", "description": "Valor atual do lançamento no sistema (para validação de concorrência)" },
            "usarValorFatura": { "type": "boolean", "description": "Se true, ajusta o valor do lançamento para o valor da fatura quando há diferença. Padrão: true." }
          },
          "required": ["faturaId", "conciliacaoId", "itemId", "contaPagarId", "valorEsperadoSistema"]
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
        if (!Guid.TryParse(input?["contaPagarId"]?.GetValue<string>(), out var contaPagarId))
            return JsonSerializer.Serialize(new { erro = "contaPagarId inválido." });

        var valorEsperado = input?["valorEsperadoSistema"]?.GetValue<decimal>() ?? 0;
        var usarValorFatura = input?["usarValorFatura"]?.GetValue<bool>() ?? true;

        var request = new VincularItemFaturaRequest(contaPagarId, valorEsperado, usarValorFatura);

        try
        {
            var ok = await conciliacao.VincularAsync(faturaId, conciliacaoId, itemId, request, cancellationToken);
            if (!ok)
                return JsonSerializer.Serialize(new { erro = "Item ou sessão de conciliação não encontrados." });

            return JsonSerializer.Serialize(new
            {
                sucesso = true,
                mensagem = "Item vinculado com sucesso ao lançamento existente. Preferências do estabelecimento foram salvas.",
                itemId,
                contaPagarId
            });
        }
        catch (Exception ex) when (ex.Message.Contains("Conciliacao") || ex.Message.Contains("conta") || ex.Message.Contains("fatura") || ex.Message.Contains("valor") || ex.Message.Contains("parcela"))
        {
            return JsonSerializer.Serialize(new { erro = ex.Message });
        }
    }
}
