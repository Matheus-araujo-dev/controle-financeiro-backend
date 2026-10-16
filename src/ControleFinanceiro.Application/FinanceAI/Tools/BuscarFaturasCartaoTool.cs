using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class BuscarFaturasCartaoTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "buscar_faturas_cartao";
    public string Description => "Lista faturas de cartão de crédito com detalhes (cartão, competência, vencimento, valor, status). Use para 'minhas faturas', 'fatura do cartão X', 'quanto devo no cartão'. Pode filtrar por mês/ano e cartão.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "mes":  { "type": "integer", "description": "Mês (1-12) da competência" },
            "ano":  { "type": "integer", "description": "Ano (ex: 2026)" },
            "cartaoId": { "type": "string", "description": "ID do cartão para filtrar (opcional)" },
            "incluirLancamentos": { "type": "boolean", "description": "Se true, inclui os lançamentos da fatura (padrão false)" }
          },
          "required": ["mes", "ano"]
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        var input = JsonNode.Parse(inputJson);
        var mes = input?["mes"]?.GetValue<int>() ?? DateTime.UtcNow.Month;
        var ano = input?["ano"]?.GetValue<int>() ?? DateTime.UtcNow.Year;
        var cartaoIdStr = input?["cartaoId"]?.GetValue<string>();
        var incluirLancamentos = input?["incluirLancamentos"]?.GetValue<bool>() ?? false;

        Guid? cartaoId = Guid.TryParse(cartaoIdStr, out var cid) ? cid : null;

        var competencia = $"{ano}-{mes:D2}";

        var query =
            from f in db.FaturasCartao.AsNoTracking()
            join c in db.Cartoes.AsNoTracking() on f.CartaoId equals c.Id
            where f.Competencia == competencia
            select new { f, CartaoNome = c.Nome };

        if (cartaoId.HasValue)
            query = query.Where(x => x.f.CartaoId == cartaoId.Value);

        var faturas = await query
            .OrderBy(x => x.f.DataVencimento)
            .Select(x => new
            {
                id = x.f.Id,
                cartao = x.CartaoNome,
                competencia = x.f.Competencia,
                fechamento = x.f.DataFechamento.ToString("dd/MM/yyyy"),
                vencimento = x.f.DataVencimento.ToString("dd/MM/yyyy"),
                valor = x.f.ValorTotal,
                status = x.f.Status == StatusFaturaCartao.Aberta ? "Aberta"
                       : x.f.Status == StatusFaturaCartao.Paga ? "Paga"
                       : "Fechada",
                dataPagamento = x.f.DataPagamento.HasValue
                    ? x.f.DataPagamento.Value.ToString("dd/MM/yyyy")
                    : null
            })
            .ToListAsync(cancellationToken);

        object? lancamentos = null;
        if (incluirLancamentos && faturas.Count > 0)
        {
            var faturaIds = faturas.Select(f => f.id).ToList();
            lancamentos = await (
                from cp in db.ContasPagar.AsNoTracking()
                join rec in db.Pessoas.AsNoTracking() on cp.RecebedorId equals rec.Id
                where cp.FaturaCartaoId.HasValue && faturaIds.Contains(cp.FaturaCartaoId!.Value)
                   && cp.StatusContaId != StatusConta.CanceladaId
                orderby cp.DataVencimento, cp.Descricao
                select new
                {
                    faturaId = cp.FaturaCartaoId,
                    descricao = cp.Descricao,
                    valor = cp.ValorLiquido,
                    vencimento = cp.DataVencimento.ToString("dd/MM/yyyy"),
                    recebedor = rec.Nome
                }
            ).Take(50).ToListAsync(cancellationToken);
        }

        return JsonSerializer.Serialize(new
        {
            competencia,
            totalFaturas = faturas.Count,
            valorTotal = faturas.Sum(f => f.valor),
            faturas,
            lancamentos
        });
    }
}
