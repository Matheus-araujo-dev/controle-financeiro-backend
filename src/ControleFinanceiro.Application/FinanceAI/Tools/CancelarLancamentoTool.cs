using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Financeiro;
using ControleFinanceiro.Domain.Identidade;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class CancelarLancamentoTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "cancelar_lancamento";
    public string Description => "Cancela uma conta a pagar ou conta a receber existente. Informe o ID e o tipo (pagar/receber). Contas liquidadas não podem ser canceladas. Chame apenas após confirmação explícita do usuário.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "id":   { "type": "string", "description": "ID do lançamento a cancelar" },
            "tipo": { "type": "string", "enum": ["pagar", "receber"], "description": "Tipo: pagar ou receber" }
          },
          "required": ["id", "tipo"]
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        if (context.Papel == PapelFamilia.Visualizador)
            return JsonSerializer.Serialize(new { erro = "Visualizadores não podem cancelar lançamentos." });

        if (context.Papel == PapelFamilia.Membro)
            return JsonSerializer.Serialize(new { erro = "Apenas administradores podem cancelar lançamentos." });

        var input = JsonNode.Parse(inputJson);
        var idStr = input?["id"]?.GetValue<string>();
        var tipo = input?["tipo"]?.GetValue<string>();

        if (!Guid.TryParse(idStr, out var id))
            return JsonSerializer.Serialize(new { erro = "ID inválido." });

        if (tipo != "pagar" && tipo != "receber")
            return JsonSerializer.Serialize(new { erro = "Tipo deve ser 'pagar' ou 'receber'." });

        try
        {
            if (tipo == "pagar")
                return await CancelarContaPagarAsync(id, cancellationToken);
            else
                return await CancelarContaReceberAsync(id, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return JsonSerializer.Serialize(new { erro = ex.Message });
        }
    }

    private async Task<string> CancelarContaPagarAsync(Guid id, CancellationToken ct)
    {
        var conta = await db.ContasPagar
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (conta is null)
            return JsonSerializer.Serialize(new { erro = "Conta a pagar não encontrada." });

        conta.Cancelar(StatusConta.CanceladaId);
        await db.SaveChangesAsync(ct);

        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            mensagem = $"Conta a pagar cancelada: {conta.Descricao} - R$ {conta.ValorLiquido:N2}."
        });
    }

    private async Task<string> CancelarContaReceberAsync(Guid id, CancellationToken ct)
    {
        var conta = await db.ContasReceber
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (conta is null)
            return JsonSerializer.Serialize(new { erro = "Conta a receber não encontrada." });

        conta.Cancelar(StatusConta.CanceladaId);
        await db.SaveChangesAsync(ct);

        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            mensagem = $"Conta a receber cancelada: {conta.Descricao} - R$ {conta.ValorLiquido:N2}."
        });
    }
}
