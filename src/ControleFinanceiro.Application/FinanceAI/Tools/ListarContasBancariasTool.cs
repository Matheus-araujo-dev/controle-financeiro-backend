using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class ListarContasBancariasTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "listar_contas_bancarias";
    public string Description => "Lista todas as contas bancárias da família, com banco, agência, número e saldo inicial.";
    public string InputSchema => """{"type":"object","properties":{"apenasAtivas":{"type":"boolean","description":"Se true (padrão), retorna apenas contas ativas."}},"required":[]}""";

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        var input = JsonNode.Parse(inputJson);
        var apenasAtivas = input?["apenasAtivas"]?.GetValue<bool>() ?? true;

        var query = db.ContasBancarias.AsNoTracking().AsQueryable();
        if (apenasAtivas)
            query = query.Where(c => c.Ativo);

        var contas = await query
            .OrderBy(c => c.Nome)
            .Select(c => new
            {
                c.Id, c.Nome, c.Banco, c.Agencia,
                c.NumeroConta, c.TipoConta, c.SaldoInicial, c.Ativo
            })
            .ToListAsync(cancellationToken);

        return JsonSerializer.Serialize(new
        {
            total = contas.Count,
            contas = contas.Select(c => new
            {
                id = c.Id,
                nome = c.Nome,
                banco = c.Banco,
                agencia = c.Agencia,
                numeroConta = c.NumeroConta,
                tipoConta = c.TipoConta,
                saldoInicial = c.SaldoInicial,
                ativo = c.Ativo
            })
        });
    }
}
