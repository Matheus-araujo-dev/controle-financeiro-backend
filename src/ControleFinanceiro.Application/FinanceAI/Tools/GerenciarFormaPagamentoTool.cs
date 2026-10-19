using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Cadastros.FormasPagamento;
using ControleFinanceiro.Domain.Identidade;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class GerenciarFormaPagamentoTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "gerenciar_forma_pagamento";
    public string Description => "Gerencia formas de pagamento: criar nova ou atualizar existente. Apenas administradores. Para listar, use listar_meios_pagamento.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "acao":                   { "type": "string",  "enum": ["criar", "atualizar"], "description": "Ação a executar" },
            "formaPagamentoId":       { "type": "string",  "description": "ID da forma de pagamento (obrigatório para atualizar)" },
            "nome":                   { "type": "string",  "description": "Nome da forma de pagamento" },
            "tipo":                   { "type": "string",  "enum": ["dinheiro", "pix", "boleto", "transferencia", "debito", "credito", "outro"], "description": "Tipo" },
            "ehCartao":               { "type": "boolean", "description": "Se é cartão (padrão: false)" },
            "baixarAutomaticamente":  { "type": "boolean", "description": "Se baixa automaticamente (padrão: false)" },
            "ativo":                  { "type": "boolean", "description": "Se está ativa (padrão: true)" }
          },
          "required": ["acao"]
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        if (context.Papel != PapelFamilia.Administrador)
            return JsonSerializer.Serialize(new { erro = "Apenas administradores podem gerenciar formas de pagamento." });

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
        var nome = input["nome"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(nome))
            return JsonSerializer.Serialize(new { erro = "Nome é obrigatório." });

        var tipo = MapearTipo(input["tipo"]?.GetValue<string>());
        if (tipo is null)
            return JsonSerializer.Serialize(new { erro = "Tipo é obrigatório. Use: dinheiro, pix, boleto, transferencia, debito, credito ou outro." });

        var ehCartao = input["ehCartao"]?.GetValue<bool>() ?? false;
        var baixar = input["baixarAutomaticamente"]?.GetValue<bool>() ?? false;
        var ativo = input["ativo"]?.GetValue<bool>() ?? true;

        var forma = FormaPagamento.Criar(nome, tipo.Value, ehCartao, baixar, ativo);

        db.FormasPagamento.Add(forma);
        await db.SaveChangesAsync(ct);

        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            formaPagamentoId = forma.Id,
            mensagem = $"Forma de pagamento '{nome}' criada com sucesso."
        });
    }

    private async Task<string> AtualizarAsync(JsonNode input, CancellationToken ct)
    {
        if (!Guid.TryParse(input["formaPagamentoId"]?.GetValue<string>(), out var id))
            return JsonSerializer.Serialize(new { erro = "formaPagamentoId é obrigatório." });

        var forma = await db.FormasPagamento.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (forma is null)
            return JsonSerializer.Serialize(new { erro = "Forma de pagamento não encontrada." });

        var nome = input["nome"]?.GetValue<string>() ?? forma.Nome;
        var tipo = MapearTipo(input["tipo"]?.GetValue<string>()) ?? forma.Tipo;
        var ehCartao = input["ehCartao"]?.GetValue<bool>() ?? forma.EhCartao;
        var baixar = input["baixarAutomaticamente"]?.GetValue<bool>() ?? forma.BaixarAutomaticamente;
        var ativo = input["ativo"]?.GetValue<bool>() ?? forma.Ativo;

        forma.Atualizar(nome, tipo, ehCartao, baixar, ativo);
        await db.SaveChangesAsync(ct);

        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            formaPagamentoId = forma.Id,
            mensagem = $"Forma de pagamento '{nome}' atualizada com sucesso."
        });
    }

    private static TipoFormaPagamento? MapearTipo(string? tipo) => tipo switch
    {
        "dinheiro" => TipoFormaPagamento.Dinheiro,
        "pix" => TipoFormaPagamento.Pix,
        "boleto" => TipoFormaPagamento.Boleto,
        "transferencia" => TipoFormaPagamento.Transferencia,
        "debito" => TipoFormaPagamento.Debito,
        "credito" => TipoFormaPagamento.Credito,
        "outro" => TipoFormaPagamento.Outro,
        _ => null
    };
}
