using System.Text.Json;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class BuscarSaldoAtualTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "buscar_saldo_atual";
    public string Description => "Retorna o saldo atual de cada conta bancária da família (SaldoInicial + movimentações realizadas). Pode filtrar por uma conta específica.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "contaBancariaId": { "type": "string", "description": "ID da conta bancária (opcional). Se omitido, retorna todas." }
          },
          "required": []
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);

        var contas = await db.ContasBancarias
            .AsNoTracking()
            .Where(c => c.Ativo)
            .Select(c => new { c.Id, c.Nome, c.SaldoInicial })
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

        var resultado = contas.Select(c =>
        {
            var movimentado = movimentadoPorConta.GetValueOrDefault(c.Id);
            return new
            {
                id = c.Id,
                nome = c.Nome,
                saldoAtual = decimal.Round(c.SaldoInicial + movimentado, 2, MidpointRounding.AwayFromZero)
            };
        });

        return JsonSerializer.Serialize(new
        {
            data = hoje.ToString("dd/MM/yyyy"),
            contas = resultado,
            totalGeral = resultado.Sum(r => r.saldoAtual)
        });
    }
}
