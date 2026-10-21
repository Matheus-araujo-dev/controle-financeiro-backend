using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Cadastros.Cartoes;
using ControleFinanceiro.Domain.Identidade;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class GerenciarCartaoTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "gerenciar_cartao";
    public string Description => "Gerencia cartões de crédito: criar novo ou atualizar existente. Para listar, use listar_cartoes.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "acao":                          { "type": "string",  "enum": ["criar", "atualizar"], "description": "Ação a executar" },
            "cartaoId":                      { "type": "string",  "description": "ID do cartão (obrigatório para atualizar)" },
            "nome":                          { "type": "string",  "description": "Nome do cartão" },
            "bandeira":                      { "type": "string",  "description": "Bandeira (Visa, Mastercard, etc.)" },
            "numeroFinal":                   { "type": "string",  "description": "4 últimos dígitos" },
            "diaFechamentoFatura":           { "type": "integer", "description": "Dia do fechamento (1-31)" },
            "diaVencimentoFatura":           { "type": "integer", "description": "Dia do vencimento (1-31)" },
            "contaBancariaPagamentoPadraoId":{ "type": "string",  "description": "ID da conta bancária para pagamento (opcional)" },
            "limiteCredito":                 { "type": "number",  "description": "Limite de crédito (opcional)" },
            "ativo":                         { "type": "boolean", "description": "Se o cartão está ativo (padrão: true)" }
          },
          "required": ["acao"]
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        if (context.Papel == PapelFamilia.Visualizador)
            return JsonSerializer.Serialize(new { erro = "Visualizadores não podem gerenciar cartões." });

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
        var bandeira = input["bandeira"]?.GetValue<string>();
        var numeroFinal = input["numeroFinal"]?.GetValue<string>();
        var diaFechamento = input["diaFechamentoFatura"]?.GetValue<int>() ?? 0;
        var diaVencimento = input["diaVencimentoFatura"]?.GetValue<int>() ?? 0;
        var ativo = input["ativo"]?.GetValue<bool>() ?? true;
        var limiteCredito = input["limiteCredito"]?.GetValue<decimal>();

        if (string.IsNullOrWhiteSpace(nome))
            return JsonSerializer.Serialize(new { erro = "Nome do cartão é obrigatório." });
        if (string.IsNullOrWhiteSpace(bandeira))
            return JsonSerializer.Serialize(new { erro = "Bandeira é obrigatória." });
        if (string.IsNullOrWhiteSpace(numeroFinal) || numeroFinal.Length != 4)
            return JsonSerializer.Serialize(new { erro = "Número final deve ter exatamente 4 dígitos." });

        Guid? contaBancariaId = null;
        var contaBancariaIdStr = input["contaBancariaPagamentoPadraoId"]?.GetValue<string>();
        if (Guid.TryParse(contaBancariaIdStr, out var cbId))
        {
            if (!await db.ContasBancarias.AsNoTracking().AnyAsync(c => c.Id == cbId, ct))
                return JsonSerializer.Serialize(new { erro = "Conta bancária de pagamento não encontrada." });
            contaBancariaId = cbId;
        }

        var cartao = Cartao.Criar(nome, bandeira, numeroFinal, diaFechamento, diaVencimento,
            contaBancariaId, limiteCredito, ativo);

        db.Cartoes.Add(cartao);
        await db.SaveChangesAsync(ct);

        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            cartaoId = cartao.Id,
            mensagem = $"Cartão '{nome}' ({bandeira} final {numeroFinal}) criado com sucesso."
        });
    }

    private async Task<string> AtualizarAsync(JsonNode input, CancellationToken ct)
    {
        if (!Guid.TryParse(input["cartaoId"]?.GetValue<string>(), out var id))
            return JsonSerializer.Serialize(new { erro = "cartaoId é obrigatório." });

        var cartao = await db.Cartoes.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (cartao is null)
            return JsonSerializer.Serialize(new { erro = "Cartão não encontrado." });

        var nome = input["nome"]?.GetValue<string>() ?? cartao.Nome;
        var bandeira = input["bandeira"]?.GetValue<string>() ?? cartao.Bandeira;
        var numeroFinal = input["numeroFinal"]?.GetValue<string>() ?? cartao.NumeroFinal;
        var diaFechamento = input["diaFechamentoFatura"]?.GetValue<int>() ?? cartao.DiaFechamentoFatura;
        var diaVencimento = input["diaVencimentoFatura"]?.GetValue<int>() ?? cartao.DiaVencimentoFatura;
        var ativo = input["ativo"]?.GetValue<bool>() ?? cartao.Ativo;
        var limiteCredito = input["limiteCredito"]?.GetValue<decimal>() ?? cartao.LimiteCredito;

        Guid? contaBancariaId = cartao.ContaBancariaPagamentoPadraoId;
        var contaBancariaIdStr = input["contaBancariaPagamentoPadraoId"]?.GetValue<string>();
        if (Guid.TryParse(contaBancariaIdStr, out var cbId))
        {
            if (!await db.ContasBancarias.AsNoTracking().AnyAsync(c => c.Id == cbId, ct))
                return JsonSerializer.Serialize(new { erro = "Conta bancária de pagamento não encontrada." });
            contaBancariaId = cbId;
        }

        cartao.Atualizar(nome, bandeira, numeroFinal, diaFechamento, diaVencimento,
            contaBancariaId, limiteCredito, ativo);

        await db.SaveChangesAsync(ct);

        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            cartaoId = cartao.Id,
            mensagem = $"Cartão '{nome}' atualizado com sucesso."
        });
    }
}
