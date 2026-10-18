using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class ProjetarFluxoCaixaTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "projetar_fluxo_caixa";
    public string Description => "Projeta o fluxo de caixa diário para os próximos dias com base nas contas a pagar e receber pendentes e no saldo atual das contas bancárias. Identifica dias com risco de saldo negativo.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "dias": { "type": "integer", "description": "Quantidade de dias para projetar (padrão 30, máximo 90)" }
          }
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        var input = JsonNode.Parse(inputJson);
        var dias = input?["dias"]?.GetValue<int>() ?? 30;
        if (dias < 1) dias = 1;
        if (dias > 90) dias = 90;

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var dataFim = hoje.AddDays(dias - 1);

        var contas = await db.ContasBancarias
            .AsNoTracking()
            .Where(c => c.Ativo)
            .Select(c => new { c.Id, c.SaldoInicial, c.DataSaldoInicial })
            .ToListAsync(cancellationToken);

        var contaIds = contas.Select(c => c.Id).ToList();

        var movimentadoPorConta = await (
            from m in db.MovimentacoesFinanceiras.AsNoTracking()
            join c in db.ContasBancarias.AsNoTracking() on m.ContaBancariaId equals c.Id
            where m.ContaBancariaId != null
                && contaIds.Contains(m.ContaBancariaId!.Value)
                && m.Natureza == NaturezaMovimentacao.Realizada
                && m.StatusMovimentacaoId != StatusMovimentacao.CanceladaId
                && m.DataMovimentacao <= hoje
                && m.DataMovimentacao >= c.DataSaldoInicial
            group m by m.ContaBancariaId!.Value into g
            select new { ContaBancariaId = g.Key, Saldo = g.Sum(m => m.Tipo == TipoMovimentacao.Entrada ? m.Valor : -m.Valor) })
            .ToDictionaryAsync(x => x.ContaBancariaId, x => x.Saldo, cancellationToken);

        var saldoContas = contas.Sum(c => c.SaldoInicial + movimentadoPorConta.GetValueOrDefault(c.Id));

        var contasPagar = await db.ContasPagar
            .AsNoTracking()
            .Where(cp => cp.DataVencimento >= hoje && cp.DataVencimento <= dataFim
                      && cp.StatusContaId == StatusConta.PendenteId)
            .Select(cp => new FluxoCaixaEvento(cp.DataVencimento, TipoMovimentacao.Saida, cp.ValorLiquido))
            .ToListAsync(cancellationToken);

        var contasReceber = await db.ContasReceber
            .AsNoTracking()
            .Where(cr => cr.DataVencimento >= hoje && cr.DataVencimento <= dataFim
                      && cr.StatusContaId == StatusConta.PendenteId)
            .Select(cr => new FluxoCaixaEvento(cr.DataVencimento, TipoMovimentacao.Entrada, cr.ValorLiquido))
            .ToListAsync(cancellationToken);

        var eventos = contasPagar.Concat(contasReceber).ToList();

        var projecao = FluxoCaixaDiario.Projetar(hoje, dias, saldoContas, eventos);

        var diasComRisco = projecao.Where(d => d.RiscoSaldoNegativo).ToList();

        var resumoDiario = projecao.Select(d => new
        {
            data = d.Data.ToString("dd/MM/yyyy"),
            saldoInicial = d.SaldoInicial,
            entradas = d.EntradasPrevistas,
            saidas = d.SaidasPrevistas,
            saldoFinal = d.SaldoFinalPrevisto,
            riscoNegativo = d.RiscoSaldoNegativo
        }).ToList();

        return JsonSerializer.Serialize(new
        {
            periodoInicio = hoje.ToString("dd/MM/yyyy"),
            periodoFim = dataFim.ToString("dd/MM/yyyy"),
            saldoInicial = saldoContas,
            totalEntradas = projecao.Sum(d => d.EntradasPrevistas),
            totalSaidas = projecao.Sum(d => d.SaidasPrevistas),
            saldoFinalPrevisto = projecao.LastOrDefault()?.SaldoFinalPrevisto ?? saldoContas,
            diasComRiscoNegativo = diasComRisco.Count,
            projecao = resumoDiario
        });
    }
}
