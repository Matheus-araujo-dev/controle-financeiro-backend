using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Cadastros.ContasBancarias;
using ControleFinanceiro.Domain.Identidade;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class GerenciarContaBancariaTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "gerenciar_conta_bancaria";
    public string Description => "Gerencia contas bancárias: criar nova ou atualizar existente. Para listar, use listar_contas_bancarias.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "acao":                        { "type": "string",  "enum": ["criar", "atualizar"], "description": "Ação a executar" },
            "contaBancariaId":             { "type": "string",  "description": "ID da conta bancária (obrigatório para atualizar)" },
            "nome":                        { "type": "string",  "description": "Nome da conta" },
            "banco":                       { "type": "string",  "description": "Nome do banco" },
            "agencia":                     { "type": "string",  "description": "Agência (opcional)" },
            "numeroConta":                 { "type": "string",  "description": "Número da conta (opcional)" },
            "tipoConta":                   { "type": "string",  "description": "Tipo: corrente, poupança, etc. (opcional)" },
            "saldoInicial":                { "type": "number",  "description": "Saldo inicial em reais" },
            "dataSaldoInicial":            { "type": "string",  "description": "Data do saldo inicial YYYY-MM-DD (padrão: hoje)" },
            "limiteCartoesCompartilhado":  { "type": "number",  "description": "Limite compartilhado de cartões (opcional)" },
            "ativo":                       { "type": "boolean", "description": "Se a conta está ativa (padrão: true)" }
          },
          "required": ["acao"]
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        if (context.Papel == PapelFamilia.Visualizador)
            return JsonSerializer.Serialize(new { erro = "Visualizadores não podem gerenciar contas bancárias." });

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
        var banco = input["banco"]?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(nome))
            return JsonSerializer.Serialize(new { erro = "Nome da conta é obrigatório." });
        if (string.IsNullOrWhiteSpace(banco))
            return JsonSerializer.Serialize(new { erro = "Nome do banco é obrigatório." });

        var agencia = input["agencia"]?.GetValue<string>();
        var numeroConta = input["numeroConta"]?.GetValue<string>();
        var tipoConta = input["tipoConta"]?.GetValue<string>();
        var saldoInicial = input["saldoInicial"]?.GetValue<decimal>() ?? 0;
        var dataSaldoInicial = DateOnly.TryParse(input["dataSaldoInicial"]?.GetValue<string>(), out var d)
            ? d : DateOnly.FromDateTime(DateTime.UtcNow);
        var limiteCompartilhado = input["limiteCartoesCompartilhado"]?.GetValue<decimal>();
        var ativo = input["ativo"]?.GetValue<bool>() ?? true;

        var conta = ContaBancaria.Criar(nome, banco, agencia, numeroConta, tipoConta,
            saldoInicial, dataSaldoInicial, limiteCompartilhado, ativo);

        db.ContasBancarias.Add(conta);
        await db.SaveChangesAsync(ct);

        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            contaBancariaId = conta.Id,
            mensagem = $"Conta bancária '{nome}' ({banco}) criada com sucesso."
        });
    }

    private async Task<string> AtualizarAsync(JsonNode input, CancellationToken ct)
    {
        if (!Guid.TryParse(input["contaBancariaId"]?.GetValue<string>(), out var id))
            return JsonSerializer.Serialize(new { erro = "contaBancariaId é obrigatório." });

        var conta = await db.ContasBancarias.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (conta is null)
            return JsonSerializer.Serialize(new { erro = "Conta bancária não encontrada." });

        var nome = input["nome"]?.GetValue<string>() ?? conta.Nome;
        var banco = input["banco"]?.GetValue<string>() ?? conta.Banco;
        var agencia = input["agencia"]?.GetValue<string>() ?? conta.Agencia;
        var numeroConta = input["numeroConta"]?.GetValue<string>() ?? conta.NumeroConta;
        var tipoConta = input["tipoConta"]?.GetValue<string>() ?? conta.TipoConta;
        var saldoInicial = input["saldoInicial"]?.GetValue<decimal>() ?? conta.SaldoInicial;
        var dataSaldoInicial = DateOnly.TryParse(input["dataSaldoInicial"]?.GetValue<string>(), out var d)
            ? d : conta.DataSaldoInicial;
        var limiteCompartilhado = input["limiteCartoesCompartilhado"]?.GetValue<decimal>() ?? conta.LimiteCartoesCompartilhado;
        var ativo = input["ativo"]?.GetValue<bool>() ?? conta.Ativo;

        conta.Atualizar(nome, banco, agencia, numeroConta, tipoConta,
            saldoInicial, dataSaldoInicial, limiteCompartilhado, ativo);

        await db.SaveChangesAsync(ct);

        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            contaBancariaId = conta.Id,
            mensagem = $"Conta bancária '{nome}' atualizada com sucesso."
        });
    }
}
