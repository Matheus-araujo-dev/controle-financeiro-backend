using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class BuscarResumoMensalTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "buscar_resumo_mensal";
    public string Description => "Retorna resumo financeiro de um mês: total a pagar, total a receber, saldo atual, maiores categorias de despesa e alertas. Use para perguntas como 'como estou esse mês?' ou 'quanto tenho a pagar?'.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "mes": { "type": "integer", "description": "Mês (1-12)" },
            "ano": { "type": "integer", "description": "Ano (ex: 2026)" }
          },
          "required": ["mes", "ano"]
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        var input = JsonNode.Parse(inputJson);
        var mes = input?["mes"]?.GetValue<int>() ?? DateTime.UtcNow.Month;
        var ano = input?["ano"]?.GetValue<int>() ?? DateTime.UtcNow.Year;

        var inicio = new DateOnly(ano, mes, 1);
        var fim = inicio.AddMonths(1).AddDays(-1);
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);

        var aPagar = await db.ContasPagar
            .AsNoTracking()
            .Where(c => c.DataVencimento >= inicio && c.DataVencimento <= fim
                     && c.StatusContaId != StatusConta.LiquidadaId
                     && c.StatusContaId != StatusConta.CanceladaId)
            .SumAsync(c => c.ValorLiquido, cancellationToken);

        var aReceber = await db.ContasReceber
            .AsNoTracking()
            .Where(c => c.DataVencimento >= inicio && c.DataVencimento <= fim
                     && c.StatusContaId != StatusConta.LiquidadaId
                     && c.StatusContaId != StatusConta.CanceladaId)
            .SumAsync(c => c.ValorLiquido, cancellationToken);

        var maioresCategorias = await (
            from r in db.RateiosContaGerencial
            join cp in db.ContasPagar on r.ContaPagarId equals cp.Id
            join cg in db.ContasGerenciais on r.ContaGerencialId equals cg.Id
            where r.ContaPagarId.HasValue
               && cp.DataVencimento >= inicio && cp.DataVencimento <= fim
               && cp.StatusContaId != StatusConta.CanceladaId
            group r by new { cg.Id, cg.Descricao } into g
            orderby g.Sum(r => r.Valor) descending
            select new { categoria = g.Key.Descricao, total = g.Sum(r => r.Valor) }
        ).Take(5).AsNoTracking().ToListAsync(cancellationToken);

        var saldoAtual = await CalcularSaldoAtualAsync(hoje, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            periodo = $"{mes:D2}/{ano}",
            saldoAtual,
            totalAPagar = aPagar,
            totalAReceber = aReceber,
            saldoProjetado = saldoAtual + aReceber - aPagar,
            maioresDespesas = maioresCategorias
        });
    }

    private async Task<decimal> CalcularSaldoAtualAsync(DateOnly ate, CancellationToken ct)
    {
        var saldoInicial = await db.ContasBancarias
            .AsNoTracking()
            .Where(c => c.Ativo)
            .SumAsync(c => c.SaldoInicial, ct);

        var movs = await db.MovimentacoesFinanceiras
            .AsNoTracking()
            .Where(m => m.StatusMovimentacaoId != StatusMovimentacao.CanceladaId
                     && m.DataMovimentacao <= ate)
            .SumAsync(m => m.Valor, ct);

        return saldoInicial + movs;
    }
}
