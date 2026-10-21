using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Cadastros.Pessoas;
using ControleFinanceiro.Domain.Identidade;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class GerenciarPessoaTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "gerenciar_pessoa";
    public string Description => "Gerencia pessoas (fornecedores, clientes, responsáveis): criar, atualizar, ativar ou inativar. Para listar, use listar_pessoas.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "acao":                      { "type": "string",  "enum": ["criar", "atualizar", "ativar", "inativar"], "description": "Ação a executar" },
            "pessoaId":                  { "type": "string",  "description": "ID da pessoa (obrigatório para atualizar/ativar/inativar)" },
            "nome":                      { "type": "string",  "description": "Nome da pessoa" },
            "tipoPessoa":                { "type": "string",  "enum": ["fisica", "juridica"], "description": "Tipo: fisica ou juridica (padrão: fisica)" },
            "cpfCnpj":                   { "type": "string",  "description": "CPF ou CNPJ (opcional)" },
            "email":                     { "type": "string",  "description": "E-mail (opcional)" },
            "telefone":                  { "type": "string",  "description": "Telefone (opcional)" },
            "observacao":                { "type": "string",  "description": "Observação (opcional)" },
            "ativo":                     { "type": "boolean", "description": "Se está ativo (padrão: true)" },
            "ehPagador":                 { "type": "boolean", "description": "Se é pagador (padrão: true)" },
            "ehRecebedor":               { "type": "boolean", "description": "Se é recebedor (padrão: true)" },
            "ehResponsavel":             { "type": "boolean", "description": "Se é responsável (padrão: true)" },
            "contaGerencialDespesaId":   { "type": "string",  "description": "Categoria padrão de despesa (opcional)" },
            "contaGerencialReceitaId":   { "type": "string",  "description": "Categoria padrão de receita (opcional)" }
          },
          "required": ["acao"]
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        if (context.Papel == PapelFamilia.Visualizador)
            return JsonSerializer.Serialize(new { erro = "Visualizadores não podem gerenciar pessoas." });

        var input = JsonNode.Parse(inputJson);
        var acao = input?["acao"]?.GetValue<string>();

        try
        {
            return acao switch
            {
                "criar" => await CriarAsync(input!, cancellationToken),
                "atualizar" => await AtualizarAsync(input!, cancellationToken),
                "ativar" => await AlterarStatusAsync(input!, true, cancellationToken),
                "inativar" => await AlterarStatusAsync(input!, false, cancellationToken),
                _ => JsonSerializer.Serialize(new { erro = "Ação inválida. Use: criar, atualizar, ativar ou inativar." })
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

        var tipoPessoa = MapearTipo(input["tipoPessoa"]?.GetValue<string>()) ?? TipoPessoa.Fisica;
        var ativo = input["ativo"]?.GetValue<bool>() ?? true;
        var ehPagador = input["ehPagador"]?.GetValue<bool>() ?? true;
        var ehRecebedor = input["ehRecebedor"]?.GetValue<bool>() ?? true;
        var ehResponsavel = input["ehResponsavel"]?.GetValue<bool>() ?? true;
        Guid? contaDespId = Guid.TryParse(input["contaGerencialDespesaId"]?.GetValue<string>(), out var cd) ? cd : null;
        Guid? contaRecId = Guid.TryParse(input["contaGerencialReceitaId"]?.GetValue<string>(), out var cr) ? cr : null;

        var pessoa = Pessoa.Criar(
            nome, tipoPessoa,
            input["cpfCnpj"]?.GetValue<string>(),
            input["email"]?.GetValue<string>(),
            input["telefone"]?.GetValue<string>(),
            input["observacao"]?.GetValue<string>(),
            Array.Empty<ChavePixPlano>(),
            ativo, ehPagador, ehRecebedor, ehResponsavel,
            contaDespId, contaRecId);

        db.Pessoas.Add(pessoa);
        await db.SaveChangesAsync(ct);

        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            pessoaId = pessoa.Id,
            mensagem = $"Pessoa '{nome}' criada com sucesso."
        });
    }

    private async Task<string> AtualizarAsync(JsonNode input, CancellationToken ct)
    {
        if (!Guid.TryParse(input["pessoaId"]?.GetValue<string>(), out var id))
            return JsonSerializer.Serialize(new { erro = "pessoaId é obrigatório." });

        var pessoa = await db.Pessoas.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (pessoa is null)
            return JsonSerializer.Serialize(new { erro = "Pessoa não encontrada." });

        var nome = input["nome"]?.GetValue<string>() ?? pessoa.Nome;
        var tipoPessoa = MapearTipo(input["tipoPessoa"]?.GetValue<string>()) ?? pessoa.TipoPessoa;
        var ativo = input["ativo"]?.GetValue<bool>() ?? pessoa.Ativo;
        var ehPagador = input["ehPagador"]?.GetValue<bool>() ?? pessoa.EhPagador;
        var ehRecebedor = input["ehRecebedor"]?.GetValue<bool>() ?? pessoa.EhRecebedor;
        var ehResponsavel = input["ehResponsavel"]?.GetValue<bool>() ?? pessoa.EhResponsavel;
        Guid? contaDespId = Guid.TryParse(input["contaGerencialDespesaId"]?.GetValue<string>(), out var cd)
            ? cd : pessoa.ContaGerencialDespesaId;
        Guid? contaRecId = Guid.TryParse(input["contaGerencialReceitaId"]?.GetValue<string>(), out var cr)
            ? cr : pessoa.ContaGerencialReceitaId;

        pessoa.AtualizarDadosBasicos(
            nome, tipoPessoa,
            input["cpfCnpj"]?.GetValue<string>() ?? pessoa.CpfCnpj,
            input["email"]?.GetValue<string>() ?? pessoa.Email,
            input["telefone"]?.GetValue<string>() ?? pessoa.Telefone,
            input["observacao"]?.GetValue<string>() ?? pessoa.Observacao,
            ativo, ehPagador, ehRecebedor, ehResponsavel,
            contaDespId, contaRecId);

        await db.SaveChangesAsync(ct);

        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            pessoaId = pessoa.Id,
            mensagem = $"Pessoa '{nome}' atualizada com sucesso."
        });
    }

    private async Task<string> AlterarStatusAsync(JsonNode input, bool ativar, CancellationToken ct)
    {
        if (!Guid.TryParse(input["pessoaId"]?.GetValue<string>(), out var id))
            return JsonSerializer.Serialize(new { erro = "pessoaId é obrigatório." });

        var pessoa = await db.Pessoas.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (pessoa is null)
            return JsonSerializer.Serialize(new { erro = "Pessoa não encontrada." });

        if (ativar) pessoa.Ativar(); else pessoa.Inativar();
        await db.SaveChangesAsync(ct);

        var status = ativar ? "ativada" : "inativada";
        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            mensagem = $"Pessoa '{pessoa.Nome}' {status} com sucesso."
        });
    }

    private static TipoPessoa? MapearTipo(string? tipo) => tipo switch
    {
        "fisica" => TipoPessoa.Fisica,
        "juridica" => TipoPessoa.Juridica,
        _ => null
    };
}
