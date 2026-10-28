using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.ImportacoesWhatsapp;
using ControleFinanceiro.Contracts.ImportacoesWhatsapp;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class GerenciarImportacaoWhatsappTool(ImportacoesWhatsappAppService service) : IFinanceTool
{
    public string Name => "gerenciar_importacao_whatsapp";
    public string Description => "Gerencia importações do WhatsApp: reprocessar (tentar extrair novamente), confirmar (aprovar importação e gerar financeiro), completar fechamento de fatura, ou reabrir para revisão.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "acao":          { "type": "string", "enum": ["reprocessar", "confirmar", "completar_fechamento", "reabrir"], "description": "Ação a executar" },
            "importacaoId":  { "type": "string", "description": "ID da importação (GUID)" },
            "recebedorFaturaId":             { "type": "string", "description": "ID da pessoa recebedora da fatura (confirmar/completar_fechamento)" },
            "pagadorFaturaId":   { "type": "string", "description": "ID do pagador da fatura (confirmar/completar_fechamento)" },
            "cartaoIds":     { "type": "array", "items": { "type": "string" }, "description": "IDs dos cartões vinculados (confirmar/completar_fechamento)" }
          },
          "required": ["acao", "importacaoId"]
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        var input = JsonNode.Parse(inputJson);
        var acao = input?["acao"]?.GetValue<string>();

        if (!Guid.TryParse(input?["importacaoId"]?.GetValue<string>(), out var importacaoId))
            return JsonSerializer.Serialize(new { erro = "importacaoId é obrigatório e deve ser um GUID válido." });

        try
        {
            return acao switch
            {
                "reprocessar" => await ReprocessarAsync(importacaoId, cancellationToken),
                "confirmar" => await ConfirmarAsync(importacaoId, input!, cancellationToken),
                "completar_fechamento" => await CompletarFechamentoAsync(importacaoId, input!, cancellationToken),
                "reabrir" => await ReabrirAsync(importacaoId, cancellationToken),
                _ => JsonSerializer.Serialize(new { erro = "Ação inválida. Use: reprocessar, confirmar, completar_fechamento ou reabrir." })
            };
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or FluentValidation.ValidationException)
        {
            return JsonSerializer.Serialize(new { erro = ex.Message });
        }
    }

    private async Task<string> ReprocessarAsync(Guid importacaoId, CancellationToken ct)
    {
        var resultado = await service.ReprocessarAsync(importacaoId, ct);
        if (resultado is null)
            return JsonSerializer.Serialize(new { erro = "Importação não encontrada." });

        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            mensagem = $"Importação reprocessada. Status: {resultado.StatusNome}.",
            status = resultado.StatusNome,
            totalItens = resultado.Itens.Count,
            mensagemErro = resultado.MensagemErro
        });
    }

    private async Task<string> ConfirmarAsync(Guid importacaoId, JsonNode input, CancellationToken ct)
    {
        var request = BuildAprovarRequest(input);
        var resultado = await service.AprovarImportacaoAsync(importacaoId, request, ct);
        if (resultado is null)
            return JsonSerializer.Serialize(new { erro = "Importação não encontrada." });

        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            mensagem = $"Importação confirmada. {resultado.Itens.Count} itens processados.",
            status = resultado.StatusNome,
            possuiGeracaoFinanceira = resultado.PossuiGeracaoFinanceira
        });
    }

    private async Task<string> CompletarFechamentoAsync(Guid importacaoId, JsonNode input, CancellationToken ct)
    {
        var request = BuildAprovarRequest(input)
            ?? new AprovarImportacaoWhatsappRequest(null, null, null);

        var resultado = await service.CompletarFechamentoFaturaAsync(importacaoId, request, ct);
        if (resultado is null)
            return JsonSerializer.Serialize(new { erro = "Importação não encontrada." });

        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            mensagem = $"Fechamento de fatura completado. Status: {resultado.StatusNome}.",
            status = resultado.StatusNome,
            possuiGeracaoFinanceira = resultado.PossuiGeracaoFinanceira
        });
    }

    private async Task<string> ReabrirAsync(Guid importacaoId, CancellationToken ct)
    {
        var resultado = await service.ReabrirImportacaoAsync(importacaoId, ct);
        if (resultado is null)
            return JsonSerializer.Serialize(new { erro = "Importação não encontrada." });

        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            mensagem = $"Importação reaberta para revisão. Status: {resultado.StatusNome}.",
            status = resultado.StatusNome
        });
    }

    private static AprovarImportacaoWhatsappRequest? BuildAprovarRequest(JsonNode input)
    {
        Guid? recebedorFaturaId = Guid.TryParse(input["recebedorFaturaId"]?.GetValue<string>(), out var rfId) ? rfId : null;
        Guid? pagadorId = Guid.TryParse(input["pagadorFaturaId"]?.GetValue<string>(), out var rpId) ? rpId : null;

        IReadOnlyCollection<Guid>? cartaoIds = null;
        var cartaoIdsNode = input["cartaoIds"]?.AsArray();
        if (cartaoIdsNode is { Count: > 0 })
        {
            cartaoIds = cartaoIdsNode
                .Select(n => Guid.TryParse(n?.GetValue<string>(), out var cid) ? cid : (Guid?)null)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToArray();
        }

        if (recebedorFaturaId is null && pagadorId is null && cartaoIds is null)
            return null;

        return new AprovarImportacaoWhatsappRequest(recebedorFaturaId, pagadorId, cartaoIds);
    }
}
