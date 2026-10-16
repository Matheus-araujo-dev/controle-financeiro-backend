using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class BuscarContasAPagarTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "buscar_contas_a_pagar";
    public string Description => "Lista contas a pagar individuais com detalhes (descrição, valor, vencimento, status, recebedor, categoria). Use para 'detalhe o que tenho a pagar', 'quais contas vencem essa semana', 'liste minhas contas a pagar'. Pode filtrar por mês/ano, status e pessoa.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "mes":  { "type": "integer", "description": "Mês (1-12)" },
            "ano":  { "type": "integer", "description": "Ano (ex: 2026)" },
            "status": { "type": "string", "description": "Filtrar por status: pendente, vencida, liquidada, em_fatura, futuro (opcional)" },
            "pessoaId": { "type": "string", "description": "ID do recebedor para filtrar (opcional)" },
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
            from cp in db.ContasPagar.AsNoTracking()
            join recebedor in db.Pessoas.AsNoTracking() on cp.RecebedorId equals recebedor.Id
            join sc in db.StatusContas.AsNoTracking() on cp.StatusContaId equals sc.Id
            where cp.DataVencimento >= inicio && cp.DataVencimento <= fim
               && cp.StatusContaId != StatusConta.CanceladaId
            select new { cp, recebedor.Nome, StatusNome = sc.Nome };

        if (statusId.HasValue)
            query = query.Where(x => x.cp.StatusContaId == statusId.Value);

        if (pessoaId.HasValue)
            query = query.Where(x => x.cp.RecebedorId == pessoaId.Value);

        var total = await query.CountAsync(cancellationToken);

        var contas = await query
            .OrderBy(x => x.cp.DataVencimento)
            .ThenBy(x => x.cp.Descricao)
            .Take(limite)
            .Select(x => new
            {
                id = x.cp.Id,
                descricao = x.cp.Descricao,
                valor = x.cp.ValorLiquido,
                vencimento = x.cp.DataVencimento.ToString("dd/MM/yyyy"),
                status = x.StatusNome,
                recebedor = x.Nome,
                parcela = x.cp.QuantidadeParcelas > 1
                    ? $"{x.cp.NumeroParcela}/{x.cp.QuantidadeParcelas}"
                    : null,
                ehRecorrente = x.cp.EhRecorrente
            })
            .ToListAsync(cancellationToken);

        var totalValor = await query.SumAsync(x => x.cp.ValorLiquido, cancellationToken);

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
        "em_fatura" => StatusConta.EmFaturaId,
        "futuro" => StatusConta.FuturoId,
        "parcial" => StatusConta.ParcialId,
        _ => null
    };
}
