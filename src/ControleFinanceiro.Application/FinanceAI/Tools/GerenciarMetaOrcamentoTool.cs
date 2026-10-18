using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Financeiro;
using ControleFinanceiro.Domain.Identidade;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class GerenciarMetaOrcamentoTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "gerenciar_meta_orcamento";
    public string Description => "Cria ou atualiza metas de orçamento por categoria e competência. Para consultar metas vs realizado, use buscar_metas_orcamento.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "acao":              { "type": "string",  "enum": ["criar", "atualizar"], "description": "Ação a executar" },
            "metaId":            { "type": "string",  "description": "ID da meta (obrigatório para atualizar)" },
            "contaGerencialId":  { "type": "string",  "description": "ID da categoria financeira (criar)" },
            "competencia":       { "type": "string",  "description": "Competência no formato yyyy-MM (criar)" },
            "valorMeta":         { "type": "number",  "description": "Valor da meta em reais (criar e atualizar)" }
          },
          "required": ["acao"]
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        if (context.Papel != PapelFamilia.Administrador)
            return JsonSerializer.Serialize(new { erro = "Apenas administradores podem gerenciar metas de orçamento." });

        var input = JsonNode.Parse(inputJson);
        var acao = input?["acao"]?.GetValue<string>();

        try
        {
            return acao switch
            {
                "criar" => await CriarAsync(input!, cancellationToken),
                "atualizar" => await AtualizarAsync(input!, cancellationToken),
                _ => JsonSerializer.Serialize(new { erro = "Ação inválida. Use: criar ou atualizar." })
            };
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

    private async Task<string> CriarAsync(JsonNode input, CancellationToken ct)
    {
        var contaGerencialIdStr = input["contaGerencialId"]?.GetValue<string>();
        var competencia = input["competencia"]?.GetValue<string>();
        var valorMeta = input["valorMeta"]?.GetValue<decimal>() ?? 0;

        if (!Guid.TryParse(contaGerencialIdStr, out var contaGerencialId))
            return JsonSerializer.Serialize(new { erro = "contaGerencialId é obrigatório e deve ser um ID válido." });

        if (string.IsNullOrWhiteSpace(competencia))
            return JsonSerializer.Serialize(new { erro = "Competência é obrigatória (formato yyyy-MM)." });

        if (valorMeta <= 0)
            return JsonSerializer.Serialize(new { erro = "Valor da meta deve ser maior que zero." });

        var contaExiste = await db.ContasGerenciais.AsNoTracking()
            .AnyAsync(c => c.Id == contaGerencialId, ct);
        if (!contaExiste)
            return JsonSerializer.Serialize(new { erro = "Categoria (conta gerencial) não encontrada. Use listar_categorias para obter o ID correto." });

        var duplicada = await db.MetasOrcamento.AsNoTracking()
            .AnyAsync(m => m.ContaGerencialId == contaGerencialId && m.Competencia == competencia.Trim(), ct);
        if (duplicada)
            return JsonSerializer.Serialize(new { erro = $"Já existe uma meta para esta categoria na competência {competencia}. Use a ação 'atualizar' para alterar o valor." });

        var meta = MetaOrcamento.Criar(contaGerencialId, competencia, valorMeta);

        db.MetasOrcamento.Add(meta);
        await db.SaveChangesAsync(ct);

        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            metaId = meta.Id,
            mensagem = $"Meta de orçamento criada: R$ {valorMeta:N2} para competência {competencia}."
        });
    }

    private async Task<string> AtualizarAsync(JsonNode input, CancellationToken ct)
    {
        if (!Guid.TryParse(input["metaId"]?.GetValue<string>(), out var id))
            return JsonSerializer.Serialize(new { erro = "metaId é obrigatório." });

        var valorMeta = input["valorMeta"]?.GetValue<decimal>();
        if (valorMeta is null || valorMeta <= 0)
            return JsonSerializer.Serialize(new { erro = "Valor da meta deve ser maior que zero." });

        var meta = await db.MetasOrcamento
            .FirstOrDefaultAsync(m => m.Id == id, ct);

        if (meta is null)
            return JsonSerializer.Serialize(new { erro = "Meta de orçamento não encontrada." });

        meta.Atualizar(valorMeta.Value);
        await db.SaveChangesAsync(ct);

        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            mensagem = $"Meta atualizada para R$ {valorMeta.Value:N2} na competência {meta.Competencia}."
        });
    }
}
