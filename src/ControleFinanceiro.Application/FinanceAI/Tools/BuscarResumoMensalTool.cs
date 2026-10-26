using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class BuscarResumoMensalTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "buscar_resumo_mensal";
    public string Description => "Retorna resumo financeiro mensal: total a pagar, total a receber, saldo projetado e maiores categorias de despesa. Use para visão geral do mês, contas a pagar pendentes ou relatório mensal. NÃO use para consultar saldo de conta bancária específica — use buscar_saldo_atual para isso.";
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
        var contas = await db.ContasBancarias
            .AsNoTracking()
            .Where(c => c.Ativo)
            .Select(c => new { c.Id, c.SaldoInicial })
            .ToListAsync(ct);

        var contaIds = contas.Select(c => c.Id).ToList();

        var movimentadoPorConta = await (
            from m in db.MovimentacoesFinanceiras.AsNoTracking()
            join c in db.ContasBancarias.AsNoTracking() on m.ContaBancariaId equals c.Id
            where m.ContaBancariaId != null
                && contaIds.Contains(m.ContaBancariaId!.Value)
                && m.Natureza == NaturezaMovimentacao.Realizada
                && m.StatusMovimentacaoId != StatusMovimentacao.CanceladaId
                && m.DataMovimentacao <= ate
                && m.DataMovimentacao >= c.DataSaldoInicial
            group m by m.ContaBancariaId!.Value into g
            select new { ContaBancariaId = g.Key, Saldo = g.Sum(m => m.Tipo == TipoMovimentacao.Entrada ? m.Valor : -m.Valor) })
            .ToDictionaryAsync(x => x.ContaBancariaId, x => x.Saldo, ct);

        return contas.Sum(c => c.SaldoInicial + movimentadoPorConta.GetValueOrDefault(c.Id));
    }
}
