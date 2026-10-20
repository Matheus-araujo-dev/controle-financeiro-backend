using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Dashboard;
using ControleFinanceiro.Contracts.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class BuscarDashboardTool(IAppDbContext db, DashboardAppService dashboardService) : IFinanceTool
{
    public string Name => "buscar_dashboard";
    public string Description => "Consulta dados do dashboard financeiro: anomalias, resumo geral, fluxo de caixa, categorias (resumo/série/lançamentos), responsáveis, central de previsão (resumo/itens) e comparativo mensal.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "consulta":          { "type": "string", "enum": ["anomalias", "resumo", "fluxo_caixa", "contas_gerenciais_resumo", "contas_gerenciais_serie", "contas_gerenciais_lancamentos", "responsaveis_resumo", "central_previsao_resumo", "central_previsao_itens", "comparativo_mensal"], "description": "Tipo de consulta" },
            "competencia":       { "type": "string", "description": "Competência YYYY-MM (padrão: mês atual)" },
            "dias":              { "type": "integer", "description": "Número de dias para projeção/análise (padrão: 30)" },
            "tipo":              { "type": "string", "description": "Tipo de conta gerencial: Despesa ou Receita (para contas_gerenciais_serie e lancamentos)" },
            "contaGerencialId":  { "type": "string", "description": "ID da conta gerencial (GUID) para filtrar série ou lançamentos" },
            "meses":             { "type": "integer", "description": "Número de meses para comparativo mensal (padrão: 6)" }
          },
          "required": ["consulta"]
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        var input = JsonNode.Parse(inputJson);
        var consulta = input?["consulta"]?.GetValue<string>();
        var compStr = input?["competencia"]?.GetValue<string>();
        var dias = input?["dias"]?.GetValue<int>() ?? 30;
        var tipo = input?["tipo"]?.GetValue<string>();
        var meses = input?["meses"]?.GetValue<int>() ?? 6;
        Guid? contaGerencialId = Guid.TryParse(input?["contaGerencialId"]?.GetValue<string>(), out var cgId) ? cgId : null;

        try
        {
            return consulta switch
            {
                "anomalias" => await AnomaliaAsync(ParseCompetencia(compStr), cancellationToken),
                "resumo" => await ResumoGeralAsync(compStr, dias, cancellationToken),
                "fluxo_caixa" => await FluxoCaixaAsync(ParseCompetencia(compStr), cancellationToken),
                "contas_gerenciais_resumo" => await ResumoCategoriasAsync(ParseCompetencia(compStr), cancellationToken),
                "contas_gerenciais_serie" => await SerieCategoriasAsync(compStr, dias, tipo, contaGerencialId, cancellationToken),
                "contas_gerenciais_lancamentos" => await LancamentosCategoriasAsync(compStr, dias, tipo, contaGerencialId, cancellationToken),
                "responsaveis_resumo" => await ResumoResponsaveisAsync(ParseCompetencia(compStr), cancellationToken),
                "central_previsao_resumo" => await CentralPrevisaoResumoAsync(compStr, dias, cancellationToken),
                "central_previsao_itens" => await CentralPrevisaoItensAsync(compStr, dias, cancellationToken),
                "comparativo_mensal" => await ComparativoMensalAsync(meses, cancellationToken),
                _ => JsonSerializer.Serialize(new { erro = "Consulta inválida. Use: anomalias, resumo, fluxo_caixa, contas_gerenciais_resumo, contas_gerenciais_serie, contas_gerenciais_lancamentos, responsaveis_resumo, central_previsao_resumo, central_previsao_itens ou comparativo_mensal." })
            };
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException)
        {
            return JsonSerializer.Serialize(new { erro = ex.Message });
        }
    }

    // ── Queries diretas (originais) ──────────────────────────────────────────

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

    // ── Queries via DashboardAppService (novas) ──────────────────────────────

    private async Task<string> ResumoGeralAsync(string? competencia, int dias, CancellationToken ct)
    {
        var response = await dashboardService.ObterResumoAsync(
            new DashboardResumoQueryRequest { MesReferencia = competencia, DiasProjetados = dias }, ct);
        return JsonSerializer.Serialize(response);
    }

    private async Task<string> SerieCategoriasAsync(string? competencia, int dias, string? tipo, Guid? contaGerencialId, CancellationToken ct)
    {
        var response = await dashboardService.ObterContasGerenciaisSerieAsync(
            new DashboardContaGerencialSerieQueryRequest { MesReferencia = competencia, Dias = dias, Tipo = tipo, ContaGerencialId = contaGerencialId }, ct);
        return JsonSerializer.Serialize(response);
    }

    private async Task<string> LancamentosCategoriasAsync(string? competencia, int dias, string? tipo, Guid? contaGerencialId, CancellationToken ct)
    {
        var response = await dashboardService.ObterContaGerencialLancamentosAsync(
            new DashboardContaGerencialLancamentosQueryRequest { MesReferencia = competencia, Dias = dias, Tipo = tipo, ContaGerencialId = contaGerencialId }, ct);
        return JsonSerializer.Serialize(response);
    }

    private async Task<string> CentralPrevisaoResumoAsync(string? competencia, int dias, CancellationToken ct)
    {
        var response = await dashboardService.ObterCentralPrevisaoResumoAsync(
            new DashboardCentralPrevisaoQueryRequest { MesReferencia = competencia, Dias = dias }, ct);
        return JsonSerializer.Serialize(response);
    }

    private async Task<string> CentralPrevisaoItensAsync(string? competencia, int dias, CancellationToken ct)
    {
        var response = await dashboardService.ObterCentralPrevisaoItensAsync(
            new DashboardCentralPrevisaoItensQueryRequest { MesReferencia = competencia, Dias = dias }, ct);
        return JsonSerializer.Serialize(response);
    }

    private async Task<string> ComparativoMensalAsync(int meses, CancellationToken ct)
    {
        var response = await dashboardService.ObterComparativoMensalAsync(
            new DashboardComparativoMensalQueryRequest { Meses = meses }, ct);
        return JsonSerializer.Serialize(response);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

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
