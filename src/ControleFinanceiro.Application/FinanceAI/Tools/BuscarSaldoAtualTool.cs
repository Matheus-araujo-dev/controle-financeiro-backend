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
            .Select(c => new { c.Id, c.Nome, c.SaldoInicial, c.DataSaldoInicial })
            .ToListAsync(cancellationToken);

        var movimentacoes = await db.MovimentacoesFinanceiras
            .AsNoTracking()
            .Where(m => m.StatusMovimentacaoId != StatusMovimentacao.CanceladaId && m.DataMovimentacao <= hoje)
            .Select(m => new { m.ContaBancariaId, m.Valor })
            .ToListAsync(cancellationToken);

        var resultado = contas.Select(c =>
        {
            var movs = movimentacoes.Where(m => m.ContaBancariaId == c.Id).Sum(m => m.Valor);
            return new
            {
                id = c.Id,
                nome = c.Nome,
                saldoAtual = c.SaldoInicial + movs
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
