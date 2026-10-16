using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class BuscarMovimentacoesTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "buscar_movimentacoes";
    public string Description => "Lista movimentações financeiras (transações) com detalhes. Use para 'extrato da conta', 'últimas movimentações', 'transações do mês', 'o que entrou/saiu na conta'. Pode filtrar por mês/ano, conta bancária e tipo (entrada/saída).";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "mes":  { "type": "integer", "description": "Mês (1-12)" },
            "ano":  { "type": "integer", "description": "Ano (ex: 2026)" },
            "contaBancariaId": { "type": "string", "description": "ID da conta bancária para filtrar (opcional)" },
            "tipo": { "type": "string", "description": "Filtrar por tipo: entrada ou saida (opcional)" },
            "limite": { "type": "integer", "description": "Quantidade máxima de registros (padrão 30)" }
          },
          "required": ["mes", "ano"]
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        var input = JsonNode.Parse(inputJson);
        var mes = input?["mes"]?.GetValue<int>() ?? DateTime.UtcNow.Month;
        var ano = input?["ano"]?.GetValue<int>() ?? DateTime.UtcNow.Year;
        var contaIdStr = input?["contaBancariaId"]?.GetValue<string>();
        var tipoStr = input?["tipo"]?.GetValue<string>();
        var limite = input?["limite"]?.GetValue<int>() ?? 30;
        if (limite > 50) limite = 50;

        Guid? contaBancariaId = Guid.TryParse(contaIdStr, out var cid) ? cid : null;
        TipoMovimentacao? tipo = tipoStr?.ToLowerInvariant() switch
        {
            "entrada" => TipoMovimentacao.Entrada,
            "saida" or "saída" => TipoMovimentacao.Saida,
            _ => null
        };

        var inicio = new DateOnly(ano, mes, 1);
        var fim = inicio.AddMonths(1).AddDays(-1);

        var query =
            from m in db.MovimentacoesFinanceiras.AsNoTracking()
            join sm in db.StatusMovimentacoes.AsNoTracking() on m.StatusMovimentacaoId equals sm.Id
            where m.DataMovimentacao >= inicio && m.DataMovimentacao <= fim
               && m.StatusMovimentacaoId != StatusMovimentacao.CanceladaId
               && m.Natureza == NaturezaMovimentacao.Realizada
            select new { m, StatusNome = sm.Nome };

        if (contaBancariaId.HasValue)
            query = query.Where(x => x.m.ContaBancariaId == contaBancariaId.Value);

        if (tipo.HasValue)
            query = query.Where(x => x.m.Tipo == tipo.Value);

        var total = await query.CountAsync(cancellationToken);

        var movimentacoes = await (
            from x in query
            join cb in db.ContasBancarias.AsNoTracking() on x.m.ContaBancariaId equals cb.Id into cbGroup
            from cb in cbGroup.DefaultIfEmpty()
            orderby x.m.DataMovimentacao descending
            select new
            {
                id = x.m.Id,
                data = x.m.DataMovimentacao.ToString("dd/MM/yyyy"),
                tipo = x.m.Tipo == TipoMovimentacao.Entrada ? "Entrada" : "Saída",
                valor = x.m.Valor,
                conta = cb != null ? cb.Nome : null,
                status = x.StatusNome,
                observacao = x.m.Observacao
            }
        ).Take(limite).ToListAsync(cancellationToken);

        var totalEntradas = await query
            .Where(x => x.m.Tipo == TipoMovimentacao.Entrada)
            .SumAsync(x => x.m.Valor, cancellationToken);

        var totalSaidas = await query
            .Where(x => x.m.Tipo == TipoMovimentacao.Saida)
            .SumAsync(x => x.m.Valor, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            periodo = $"{mes:D2}/{ano}",
            totalRegistros = total,
            exibindo = movimentacoes.Count,
            totalEntradas,
            totalSaidas,
            saldoPeriodo = totalEntradas - totalSaidas,
            movimentacoes
        });
    }
}
