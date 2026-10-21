using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Financeiro;
using ControleFinanceiro.Domain.Identidade;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class LiquidarLancamentoTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "liquidar_lancamento";
    public string Description => "Marca uma conta a pagar como paga (liquidada) ou uma conta a receber como recebida. Registra a data e a conta bancária usada.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "tipo":             { "type": "string", "enum": ["pagar", "receber"], "description": "Tipo do lançamento" },
            "lancamentoId":     { "type": "string", "description": "ID do lançamento a liquidar" },
            "contaBancariaId":  { "type": "string", "description": "ID da conta bancária usada no pagamento/recebimento" },
            "dataLiquidacao":   { "type": "string", "description": "Data no formato YYYY-MM-DD. Se omitida, usa hoje." }
          },
          "required": ["tipo", "lancamentoId", "contaBancariaId"]
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        if (context.Papel == PapelFamilia.Visualizador)
            return JsonSerializer.Serialize(new { erro = "Visualizadores não podem liquidar lançamentos." });

        var input = JsonNode.Parse(inputJson);
        var tipo = input?["tipo"]?.GetValue<string>();
        var lancamentoIdStr = input?["lancamentoId"]?.GetValue<string>();
        var contaBancariaIdStr = input?["contaBancariaId"]?.GetValue<string>();
        var dataLiquidacaoStr = input?["dataLiquidacao"]?.GetValue<string>();

        if (tipo != "pagar" && tipo != "receber")
            return JsonSerializer.Serialize(new { erro = "Tipo deve ser 'pagar' ou 'receber'." });

        if (!Guid.TryParse(lancamentoIdStr, out var lancamentoId))
            return JsonSerializer.Serialize(new { erro = "lancamentoId inválido." });

        if (!Guid.TryParse(contaBancariaIdStr, out var contaBancariaId))
            return JsonSerializer.Serialize(new { erro = "contaBancariaId inválido." });

        var contaBancariaExiste = await db.ContasBancarias
            .AsNoTracking()
            .AnyAsync(c => c.Id == contaBancariaId, cancellationToken);

        if (!contaBancariaExiste)
            return JsonSerializer.Serialize(new { erro = "Conta bancária não encontrada." });

        var dataLiquidacao = DateOnly.TryParse(dataLiquidacaoStr, out var dl)
            ? dl
            : DateOnly.FromDateTime(DateTime.UtcNow);

        try
        {
            if (tipo == "pagar")
                return await LiquidarContaPagarAsync(lancamentoId, dataLiquidacao, contaBancariaId, cancellationToken);
            else
                return await LiquidarContaReceberAsync(lancamentoId, dataLiquidacao, contaBancariaId, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return JsonSerializer.Serialize(new { erro = ex.Message });
        }
    }

    private async Task<string> LiquidarContaPagarAsync(
        Guid id, DateOnly dataLiquidacao, Guid contaBancariaId, CancellationToken ct)
    {
        var conta = await db.ContasPagar
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (conta is null)
            return JsonSerializer.Serialize(new { erro = "Conta a pagar não encontrada." });

        conta.Liquidar(dataLiquidacao, contaBancariaId, StatusConta.LiquidadaId);
        await db.SaveChangesAsync(ct);

        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            mensagem = $"Conta a pagar liquidada: {conta.Descricao} - R$ {conta.ValorLiquido:N2} em {dataLiquidacao:dd/MM/yyyy}."
        });
    }

    private async Task<string> LiquidarContaReceberAsync(
        Guid id, DateOnly dataLiquidacao, Guid contaBancariaId, CancellationToken ct)
    {
        var conta = await db.ContasReceber
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (conta is null)
            return JsonSerializer.Serialize(new { erro = "Conta a receber não encontrada." });

        conta.Liquidar(dataLiquidacao, contaBancariaId, StatusConta.LiquidadaId);
        await db.SaveChangesAsync(ct);

        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            mensagem = $"Conta a receber liquidada: {conta.Descricao} - R$ {conta.ValorLiquido:N2} em {dataLiquidacao:dd/MM/yyyy}."
        });
    }
}
