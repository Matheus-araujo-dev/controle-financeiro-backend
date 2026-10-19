using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class BuscarDashboardTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "buscar_dashboard";
    public string Description => "Consulta dados do dashboard financeiro: anomalias de gastos, fluxo de caixa diário, resumo por categoria ou resumo por responsável.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "consulta":     { "type": "string", "enum": ["anomalias", "fluxo_caixa", "contas_gerenciais_resumo", "responsaveis_resumo"], "description": "Tipo de consulta" },
            "competencia":  { "type": "string", "description": "Competência YYYY-MM (padrão: mês atual)" }
          },
          "required": ["consulta"]
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        var input = JsonNode.Parse(inputJson);
        var consulta = input?["consulta"]?.GetValue<string>();
        var compStr = input?["competencia"]?.GetValue<string>();
        var competencia = ParseCompetencia(compStr);

        return consulta switch
        {
            "anomalias" => await AnomaliaAsync(competencia, cancellationToken),
            "fluxo_caixa" => await FluxoCaixaAsync(competencia, cancellationToken),
            "contas_gerenciais_resumo" => await ResumoCategoriasAsync(competencia, cancellationToken),
            "responsaveis_resumo" => await ResumoResponsaveisAsync(competencia, cancellationToken),
            _ => JsonSerializer.Serialize(new { erro = "Consulta inválida. Use: anomalias, fluxo_caixa, contas_gerenciais_resumo ou responsaveis_resumo." })
        };
    }

    private async Task<string> AnomaliaAsync((DateOnly inicio, DateOnly fim) periodo, CancellationToken ct)
    {
        var contas = await db.ContasPagar
            .AsNoTracking()
            .Where(c => c.DataVencimento >= periodo.inicio && c.DataVencimento <= periodo.fim)
            .Select(c => new { c.Id, c.Descricao, c.ValorLiquido, c.ContaBancariaId, RateioIds = c.Rateios.Select(r => r.ContaGerencialId).ToList() })
            .ToListAsync(ct);

        if (contas.Count == 0)
            return JsonSerializer.Serialize(new { total = 0, anomalias = Array.Empty<object>(), mensagem = "Nenhuma conta encontrada no período." });

        var mediaPorCategoria = contas
            .SelectMany(c => c.RateioIds.Select(cgId => new { cgId, c.ValorLiquido }))
            .GroupBy(x => x.cgId)
            .ToDictionary(g => g.Key, g => g.Average(x => x.ValorLiquido));

        var anomalias = contas
            .Where(c => c.RateioIds.Any(cgId =>
                mediaPorCategoria.TryGetValue(cgId, out var media) && c.ValorLiquido > media * 2))
            .OrderByDescending(c => c.ValorLiquido)
            .Take(10)
            .Select(c => new { c.Id, c.Descricao, valor = c.ValorLiquido })
            .ToList();

        return JsonSerializer.Serialize(new
        {
            periodo = $"{periodo.inicio:yyyy-MM-dd} a {periodo.fim:yyyy-MM-dd}",
            total = anomalias.Count,
            anomalias
        });
    }

    private async Task<string> FluxoCaixaAsync((DateOnly inicio, DateOnly fim) periodo, CancellationToken ct)
    {
        var pagar = await db.ContasPagar
            .AsNoTracking()
            .Where(c => c.DataVencimento >= periodo.inicio && c.DataVencimento <= periodo.fim)
            .GroupBy(c => c.DataVencimento)
            .Select(g => new { Data = g.Key, Saidas = g.Sum(c => c.ValorLiquido) })
            .ToListAsync(ct);

        var receber = await db.ContasReceber
            .AsNoTracking()
            .Where(c => c.DataVencimento >= periodo.inicio && c.DataVencimento <= periodo.fim)
            .GroupBy(c => c.DataVencimento)
            .Select(g => new { Data = g.Key, Entradas = g.Sum(c => c.ValorLiquido) })
            .ToListAsync(ct);

        var todasDatas = pagar.Select(p => p.Data)
            .Union(receber.Select(r => r.Data))
            .OrderBy(d => d)
            .ToList();

        var pagarDict = pagar.ToDictionary(p => p.Data, p => p.Saidas);
        var receberDict = receber.ToDictionary(r => r.Data, r => r.Entradas);

        var fluxo = todasDatas.Select(d =>
        {
            var entradas = receberDict.GetValueOrDefault(d, 0);
            var saidas = pagarDict.GetValueOrDefault(d, 0);
            return new
            {
                data = d.ToString("yyyy-MM-dd"),
                entradas,
                saidas,
                saldo = entradas - saidas
            };
        }).ToList();

        return JsonSerializer.Serialize(new
        {
            periodo = $"{periodo.inicio:yyyy-MM-dd} a {periodo.fim:yyyy-MM-dd}",
            totalEntradas = fluxo.Sum(f => f.entradas),
            totalSaidas = fluxo.Sum(f => f.saidas),
            dias = fluxo
        });
    }

    private async Task<string> ResumoCategoriasAsync((DateOnly inicio, DateOnly fim) periodo, CancellationToken ct)
    {
        var rateios = await db.ContasPagar
            .AsNoTracking()
            .Where(c => c.DataVencimento >= periodo.inicio && c.DataVencimento <= periodo.fim)
            .SelectMany(c => c.Rateios)
            .GroupBy(r => r.ContaGerencialId)
            .Select(g => new { ContaGerencialId = g.Key, Total = g.Sum(r => r.Valor) })
            .ToListAsync(ct);

        var catIds = rateios.Select(r => r.ContaGerencialId).ToHashSet();
        var categorias = await db.ContasGerenciais
            .AsNoTracking()
            .Where(c => catIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => new { c.Descricao, Tipo = c.Tipo.ToString() }, ct);

        var resultado = rateios
            .OrderByDescending(r => r.Total)
            .Select(r => new
            {
                categoriaId = r.ContaGerencialId,
                descricao = categorias.TryGetValue(r.ContaGerencialId, out var cat) ? cat.Descricao : "Desconhecida",
                tipo = cat?.Tipo ?? "Desconhecido",
                total = r.Total
            })
            .ToList();

        return JsonSerializer.Serialize(new
        {
            periodo = $"{periodo.inicio:yyyy-MM-dd} a {periodo.fim:yyyy-MM-dd}",
            totalCategorias = resultado.Count,
            categorias = resultado
        });
    }

    private async Task<string> ResumoResponsaveisAsync((DateOnly inicio, DateOnly fim) periodo, CancellationToken ct)
    {
        var gastos = await db.ContasPagar
            .AsNoTracking()
            .Where(c => c.DataVencimento >= periodo.inicio && c.DataVencimento <= periodo.fim
                && c.ResponsavelCompraId != null)
            .GroupBy(c => c.ResponsavelCompraId!.Value)
            .Select(g => new { ResponsavelId = g.Key, Total = g.Sum(c => c.ValorLiquido), Quantidade = g.Count() })
            .ToListAsync(ct);

        var pessoaIds = gastos.Select(g => g.ResponsavelId).ToHashSet();
        var pessoas = await db.Pessoas
            .AsNoTracking()
            .Where(p => pessoaIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Nome, ct);

        var resultado = gastos
            .OrderByDescending(g => g.Total)
            .Select(g => new
            {
                responsavelId = g.ResponsavelId,
                nome = pessoas.GetValueOrDefault(g.ResponsavelId, "Desconhecido"),
                total = g.Total,
                quantidade = g.Quantidade
            })
            .ToList();

        return JsonSerializer.Serialize(new
        {
            periodo = $"{periodo.inicio:yyyy-MM-dd} a {periodo.fim:yyyy-MM-dd}",
            totalResponsaveis = resultado.Count,
            responsaveis = resultado
        });
    }

    private static (DateOnly inicio, DateOnly fim) ParseCompetencia(string? competencia)
    {
        if (!string.IsNullOrWhiteSpace(competencia) && competencia.Length == 7
            && int.TryParse(competencia[..4], out var ano) && int.TryParse(competencia[5..], out var mes))
        {
            var inicio = new DateOnly(ano, mes, 1);
            var fim = inicio.AddMonths(1).AddDays(-1);
            return (inicio, fim);
        }

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var inicioMes = new DateOnly(hoje.Year, hoje.Month, 1);
        var fimMes = inicioMes.AddMonths(1).AddDays(-1);
        return (inicioMes, fimMes);
    }
}
