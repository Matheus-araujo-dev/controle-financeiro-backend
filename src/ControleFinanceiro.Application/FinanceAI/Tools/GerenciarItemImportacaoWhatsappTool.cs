using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.ImportacoesWhatsapp;
using ControleFinanceiro.Contracts.ImportacoesWhatsapp;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class GerenciarItemImportacaoWhatsappTool(ImportacoesWhatsappAppService service) : IFinanceTool
{
    public string Name => "gerenciar_item_importacao_whatsapp";
    public string Description => "Confirma ou rejeita um item individual de uma importação WhatsApp. Permite ajustar descrição, conta gerencial, responsável e configurações antes de confirmar.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "acao":                 { "type": "string",  "enum": ["confirmar", "rejeitar"], "description": "Ação a executar" },
            "itemId":               { "type": "string",  "description": "ID do item importado (GUID)" },
            "observacao":           { "type": "string",  "description": "Observação livre (opcional)" },
            "descricaoAjustada":    { "type": "string",  "description": "Descrição ajustada para o lançamento (opcional)" },
            "contaGerencialId":     { "type": "string",  "description": "ID da conta gerencial/categoria (GUID, opcional)" },
            "responsavelId":        { "type": "string",  "description": "ID do responsável (GUID, opcional)" },
            "dataVencimentoContaReceber": { "type": "string", "description": "Data de vencimento da conta a receber (YYYY-MM-DD, se gerar)" },
            "gerarContaReceber":    { "type": "boolean", "description": "Se deve gerar conta a receber vinculada (padrão false)" },
            "marcarComoRecorrente": { "type": "boolean", "description": "Se deve marcar como recorrente para predições futuras (padrão false)" }
          },
          "required": ["acao", "itemId"]
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        var input = JsonNode.Parse(inputJson);
        var acao = input?["acao"]?.GetValue<string>();

        if (!Guid.TryParse(input?["itemId"]?.GetValue<string>(), out var itemId))
            return JsonSerializer.Serialize(new { erro = "itemId é obrigatório e deve ser um GUID válido." });

        var request = BuildRequest(input!);

        try
        {
            var resultado = acao switch
            {
                "confirmar" => await service.ConfirmarItemAsync(itemId, request, cancellationToken),
                "rejeitar" => await service.RejeitarItemAsync(itemId, request, cancellationToken),
                _ => null
            };

            if (acao is not "confirmar" and not "rejeitar")
                return JsonSerializer.Serialize(new { erro = "Ação inválida. Use: confirmar ou rejeitar." });

            if (resultado is null)
                return JsonSerializer.Serialize(new { erro = "Item não encontrado." });

            var itensPendentes = resultado.Itens.Count(i => i.StatusCodigo == "Pendente" || i.StatusCodigo == "PendenteRevisao");

            return JsonSerializer.Serialize(new
            {
                sucesso = true,
                mensagem = acao == "confirmar"
                    ? $"Item confirmado com sucesso. {itensPendentes} itens pendentes restantes."
                    : $"Item rejeitado. {itensPendentes} itens pendentes restantes.",
                importacaoStatus = resultado.StatusNome,
                itensPendentes,
                totalItens = resultado.Itens.Count
            });
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or FluentValidation.ValidationException)
        {
            return JsonSerializer.Serialize(new { erro = ex.Message });
        }
    }

    private static RevisarItemImportadoWhatsappRequest BuildRequest(JsonNode input)
    {
        Guid? contaGerencialId = Guid.TryParse(input["contaGerencialId"]?.GetValue<string>(), out var cgId) ? cgId : null;
        Guid? responsavelId = Guid.TryParse(input["responsavelId"]?.GetValue<string>(), out var rId) ? rId : null;

        DateOnly? dataVencimento = null;
        var dataStr = input["dataVencimentoContaReceber"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(dataStr) && DateOnly.TryParse(dataStr, out var dv))
            dataVencimento = dv;

        return new RevisarItemImportadoWhatsappRequest(
            Observacao: input["observacao"]?.GetValue<string>(),
            DescricaoAjustada: input["descricaoAjustada"]?.GetValue<string>(),
            ContaGerencialId: contaGerencialId,
            ResponsavelId: responsavelId,
            DataVencimentoContaReceber: dataVencimento,
            GerarContaReceber: input["gerarContaReceber"]?.GetValue<bool>() ?? false,
            MarcarComoRecorrente: input["marcarComoRecorrente"]?.GetValue<bool>() ?? false);
    }
}
