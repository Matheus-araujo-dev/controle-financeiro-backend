using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class BuscarContasAReceberTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "buscar_contas_a_receber";
    public string Description => "Lista contas a receber individuais com detalhes (descrição, valor, vencimento, status, pagador). Use para 'detalhe o que tenho a receber', 'quais recebimentos tenho esse mês', 'liste minhas contas a receber'. Pode filtrar por mês/ano, status e pessoa.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "mes":  { "type": "integer", "description": "Mês (1-12)" },
            "ano":  { "type": "integer", "description": "Ano (ex: 2026)" },
            "status": { "type": "string", "description": "Filtrar por status: pendente, vencida, liquidada, futuro (opcional)" },
            "pessoaId": { "type": "string", "description": "ID do pagador para filtrar (opcional)" },
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
        var statusStr = input?["status"]?.GetValue<string>();
        var pessoaIdStr = input?["pessoaId"]?.GetValue<string>();
        var limite = input?["limite"]?.GetValue<int>() ?? 30;
        if (limite > 50) limite = 50;

        Guid? pessoaId = Guid.TryParse(pessoaIdStr, out var pid) ? pid : null;
        Guid? statusId = MapStatus(statusStr);

        var inicio = new DateOnly(ano, mes, 1);
        var fim = inicio.AddMonths(1).AddDays(-1);

        var query =
            from cr in db.ContasReceber.AsNoTracking()
            join pagador in db.Pessoas.AsNoTracking() on cr.PagadorId equals pagador.Id
            join sc in db.StatusContas.AsNoTracking() on cr.StatusContaId equals sc.Id
            where cr.DataVencimento >= inicio && cr.DataVencimento <= fim
               && cr.StatusContaId != StatusConta.CanceladaId
            select new { cr, pagador.Nome, StatusNome = sc.Nome };

        if (statusId.HasValue)
            query = query.Where(x => x.cr.StatusContaId == statusId.Value);

        if (pessoaId.HasValue)
            query = query.Where(x => x.cr.PagadorId == pessoaId.Value);

        var total = await query.CountAsync(cancellationToken);

        var contas = await query
            .OrderBy(x => x.cr.DataVencimento)
            .ThenBy(x => x.cr.Descricao)
            .Take(limite)
            .Select(x => new
            {
                id = x.cr.Id,
                descricao = x.cr.Descricao,
                valor = x.cr.ValorLiquido,
                vencimento = x.cr.DataVencimento.ToString("dd/MM/yyyy"),
                status = x.StatusNome,
                pagador = x.Nome,
                parcela = x.cr.QuantidadeParcelas > 1
                    ? $"{x.cr.NumeroParcela}/{x.cr.QuantidadeParcelas}"
                    : null,
                ehRecorrente = x.cr.EhRecorrente
            })
            .ToListAsync(cancellationToken);

        var totalValor = await query.SumAsync(x => x.cr.ValorLiquido, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            periodo = $"{mes:D2}/{ano}",
            totalRegistros = total,
            exibindo = contas.Count,
            totalValor,
            contas
        });
    }

    private static Guid? MapStatus(string? status) => status?.ToLowerInvariant() switch
    {
        "pendente" => StatusConta.PendenteId,
        "liquidada" => StatusConta.LiquidadaId,
        "vencida" => StatusConta.VencidaId,
        "futuro" => StatusConta.FuturoId,
        "parcial" => StatusConta.ParcialId,
        _ => null
    };
}
