using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class ListarCartoesTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "listar_cartoes";
    public string Description => "Lista todos os cartões de crédito da família, com bandeira, últimos dígitos, dias de fechamento/vencimento e limite.";
    public string InputSchema => """{"type":"object","properties":{"apenasAtivos":{"type":"boolean","description":"Se true (padrão), retorna apenas cartões ativos."}},"required":[]}""";

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        var input = JsonNode.Parse(inputJson);
        var apenasAtivos = input?["apenasAtivos"]?.GetValue<bool>() ?? true;

        var query = db.Cartoes.AsNoTracking().AsQueryable();
        if (apenasAtivos)
            query = query.Where(c => c.Ativo);

        var cartoes = await query
            .OrderBy(c => c.Nome)
            .Select(c => new
            {
                c.Id, c.Nome, c.Bandeira, c.NumeroFinal,
                c.DiaFechamentoFatura, c.DiaVencimentoFatura,
                c.LimiteCredito, c.Ativo
            })
            .ToListAsync(cancellationToken);

        return JsonSerializer.Serialize(new
        {
            total = cartoes.Count,
            cartoes = cartoes.Select(c => new
            {
                id = c.Id,
                nome = c.Nome,
                bandeira = c.Bandeira,
                numeroFinal = c.NumeroFinal,
                diaFechamento = c.DiaFechamentoFatura,
                diaVencimento = c.DiaVencimentoFatura,
                limiteCredito = c.LimiteCredito,
                ativo = c.Ativo
            })
        });
    }
}
