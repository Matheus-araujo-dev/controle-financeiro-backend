using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;
using ControleFinanceiro.Domain.Identidade;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class GerenciarCategoriaTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "gerenciar_categoria";
    public string Description => "Gerencia categorias financeiras (contas gerenciais): criar nova ou atualizar existente. Apenas administradores. Para listar, use listar_categorias.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "acao":                            { "type": "string",  "enum": ["criar", "atualizar"], "description": "Ação a executar" },
            "categoriaId":                     { "type": "string",  "description": "ID da categoria (obrigatório para atualizar)" },
            "codigo":                          { "type": "string",  "description": "Código da categoria (opcional)" },
            "descricao":                       { "type": "string",  "description": "Descrição/nome da categoria" },
            "tipo":                            { "type": "string",  "enum": ["receita", "despesa"], "description": "Tipo: receita ou despesa" },
            "contaPaiId":                      { "type": "string",  "description": "ID da categoria pai (opcional, para subcategorias)" },
            "membroPadraoId":             { "type": "string",  "description": "ID do membro padrão (opcional)" },
            "ativo":                           { "type": "boolean", "description": "Se está ativa (padrão: true)" },
            "ehPadraoRecebimentoFaturaCartao": { "type": "boolean", "description": "Se é padrão para recebimento de fatura (apenas receita)" }
          },
          "required": ["acao"]
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        if (context.Papel != PapelFamilia.Administrador)
            return JsonSerializer.Serialize(new { erro = "Apenas administradores podem gerenciar categorias." });

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
        var descricao = input["descricao"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(descricao))
            return JsonSerializer.Serialize(new { erro = "Descrição da categoria é obrigatória." });

        var tipo = MapearTipo(input["tipo"]?.GetValue<string>());
        if (tipo is null)
            return JsonSerializer.Serialize(new { erro = "Tipo é obrigatório. Use: receita ou despesa." });

        var codigo = input["codigo"]?.GetValue<string>();
        var ativo = input["ativo"]?.GetValue<bool>() ?? true;
        var ehPadrao = input["ehPadraoRecebimentoFaturaCartao"]?.GetValue<bool>() ?? false;
        Guid? contaPaiId = Guid.TryParse(input["contaPaiId"]?.GetValue<string>(), out var cp) ? cp : null;
        Guid? membroId = Guid.TryParse(input["membroPadraoId"]?.GetValue<string>(), out var rp) ? rp : null;

        var categoria = ContaGerencial.Criar(codigo, descricao, tipo.Value, contaPaiId,
            membroId, ativo, ehPadrao);

        db.ContasGerenciais.Add(categoria);
        await db.SaveChangesAsync(ct);

        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            categoriaId = categoria.Id,
            mensagem = $"Categoria '{descricao}' ({tipo.Value}) criada com sucesso."
        });
    }

    private async Task<string> AtualizarAsync(JsonNode input, CancellationToken ct)
    {
        if (!Guid.TryParse(input["categoriaId"]?.GetValue<string>(), out var id))
            return JsonSerializer.Serialize(new { erro = "categoriaId é obrigatório." });

        var categoria = await db.ContasGerenciais.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (categoria is null)
            return JsonSerializer.Serialize(new { erro = "Categoria não encontrada." });

        var descricao = input["descricao"]?.GetValue<string>() ?? categoria.Descricao;
        var tipo = MapearTipo(input["tipo"]?.GetValue<string>()) ?? categoria.Tipo;
        var codigo = input["codigo"]?.GetValue<string>() ?? categoria.Codigo;
        var ativo = input["ativo"]?.GetValue<bool>() ?? categoria.Ativo;
        var ehPadrao = input["ehPadraoRecebimentoFaturaCartao"]?.GetValue<bool>() ?? categoria.EhPadraoRecebimentoFaturaCartao;
        Guid? contaPaiId = Guid.TryParse(input["contaPaiId"]?.GetValue<string>(), out var cp) ? cp : categoria.ContaPaiId;
        Guid? membroId = Guid.TryParse(input["membroPadraoId"]?.GetValue<string>(), out var rp) ? rp : categoria.MembroPadraoId;

        categoria.Atualizar(codigo, descricao, tipo, contaPaiId, membroId, ativo, ehPadrao);
        await db.SaveChangesAsync(ct);

        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            categoriaId = categoria.Id,
            mensagem = $"Categoria '{descricao}' atualizada com sucesso."
        });
    }

    private static TipoContaGerencial? MapearTipo(string? tipo) => tipo switch
    {
        "receita" => TipoContaGerencial.Receita,
        "despesa" => TipoContaGerencial.Despesa,
        _ => null
    };
}
