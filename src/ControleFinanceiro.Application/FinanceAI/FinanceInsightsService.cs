using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Contracts.Agente;
using ControleFinanceiro.Domain.Financeiro;
using ControleFinanceiro.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json.Nodes;

namespace ControleFinanceiro.Application.FinanceAI;

public sealed class FinanceInsightsService(
    ILlmClient llmClient,
    IAppDbContext db,
    ICurrentUser currentUser,
    IMemoryCache cache,
    ILogger<FinanceInsightsService> logger)
{
    public async Task<AgenteInsightsResponse> GerarInsightsAsync(
        string mesReferencia, CancellationToken cancellationToken)
    {
        var familiaId = currentUser.FamiliaId
            ?? throw new InvalidOperationException("Família não identificada.");

        var cacheKey = $"insights:{familiaId}:{mesReferencia}";
        if (cache.TryGetValue(cacheKey, out AgenteInsightsResponse? cached) && cached is not null)
            return cached;

        var contexto = await MontarContextoAsync(familiaId, mesReferencia, cancellationToken);

        var systemPrompt = """
            Você é um analista financeiro pessoal. Analise os dados financeiros fornecidos e gere
            exatamente 4 insights curtos, objetivos e acionáveis em português brasileiro.

            Retorne SOMENTE um JSON (sem markdown, sem explicações):
            {"insights":[{"tipo":"ALERTA|POSITIVO|DICA|INFO","mensagem":"texto até 120 chars","valor":"R$ XX (opcional)"}]}

            Tipos:
            - ALERTA: algo preocupante que requer ação (saldo baixo, contas vencidas, gastos altos)
            - POSITIVO: conquista ou tendência boa (meta cumprida, saldo positivo, receitas crescendo)
            - DICA: sugestão prática baseada nos dados
            - INFO: fato relevante sem conotação positiva/negativa

            Seja específico com valores e categorias. Não invente dados que não foram fornecidos.
            """;

        var userMessage = $"Dados financeiros de {mesReferencia}:\n\n{contexto}";
        var messages = new List<LlmMessage> { new(LlmRole.User, userMessage) };

        try
        {
            var request = new LlmRequest(LlmModelTier.Reasoning, systemPrompt, messages);
            var completion = await llmClient.CompleteAsync(request, cancellationToken);
            var insights = ParseInsights(completion.Text);

            var response = new AgenteInsightsResponse(insights, completion.Usage.InputTokens + completion.Usage.OutputTokens);

            cache.Set(cacheKey, response, TimeSpan.FromHours(1));
            logger.LogInformation("Insights gerados para família {FamiliaId} mês {Mes}: {Count} insights", familiaId, mesReferencia, insights.Count);

            return response;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao gerar insights para {FamiliaId}", familiaId);
            return new AgenteInsightsResponse([], 0);
        }
    }

    private async Task<string> MontarContextoAsync(Guid familiaId, string mesReferencia, CancellationToken ct)
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        DateOnly.TryParseExact(mesReferencia + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var inicioMes);
        if (inicioMes == default) inicioMes = new DateOnly(hoje.Year, hoje.Month, 1);
        var fimMes = inicioMes.AddMonths(1).AddDays(-1);

        var contasPagarMes = await db.ContasPagar
            .AsNoTracking()
            .Where(c => c.FamiliaId == familiaId && c.DataVencimento >= inicioMes && c.DataVencimento <= fimMes)
            .Select(c => new { c.ValorLiquido, c.DataVencimento, StatusId = c.StatusContaId })
            .ToListAsync(ct);

        var contasReceberMes = await db.ContasReceber
            .AsNoTracking()
            .Where(c => c.FamiliaId == familiaId && c.DataVencimento >= inicioMes && c.DataVencimento <= fimMes)
            .Select(c => new { c.ValorLiquido, StatusId = c.StatusContaId })
            .ToListAsync(ct);

        var contasVencidas = contasPagarMes
            .Where(c => c.DataVencimento < hoje && c.StatusId != StatusConta.LiquidadaId)
            .ToList();

        var totalDespesas = contasPagarMes.Where(c => c.StatusId == StatusConta.LiquidadaId).Sum(c => c.ValorLiquido);
        var totalReceitas = contasReceberMes.Where(c => c.StatusId == StatusConta.LiquidadaId).Sum(c => c.ValorLiquido);
        var aPagar = contasPagarMes.Where(c => c.StatusId != StatusConta.LiquidadaId).Sum(c => c.ValorLiquido);
        var aReceber = contasReceberMes.Where(c => c.StatusId != StatusConta.LiquidadaId).Sum(c => c.ValorLiquido);

        // Top 3 categorias de despesa liquidadas no mês via join simples
        var topCategorias = await db.ContasPagar
            .AsNoTracking()
            .Where(cp => cp.FamiliaId == familiaId
                      && cp.DataVencimento >= inicioMes
                      && cp.DataVencimento <= fimMes
                      && cp.StatusContaId == StatusConta.LiquidadaId)
            .Join(db.RateiosContaGerencial, cp => cp.Id, r => r.ContaPagarId,
                (cp, r) => new { r.ContaGerencialId, r.Valor })
            .Join(db.ContasGerenciais, r => r.ContaGerencialId, cg => cg.Id,
                (r, cg) => new { cg.Descricao, r.Valor })
            .GroupBy(x => x.Descricao)
            .Select(g => new { Categoria = g.Key, Total = g.Sum(x => x.Valor) })
            .OrderByDescending(g => g.Total)
            .Take(3)
            .ToListAsync(ct);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Mês: {mesReferencia}");
        sb.AppendLine($"Despesas realizadas: R$ {totalDespesas:N2}");
        sb.AppendLine($"Receitas realizadas: R$ {totalReceitas:N2}");
        sb.AppendLine($"Saldo do mês: R$ {totalReceitas - totalDespesas:N2}");
        sb.AppendLine($"A pagar ainda: R$ {aPagar:N2}");
        sb.AppendLine($"A receber ainda: R$ {aReceber:N2}");
        sb.AppendLine($"Contas vencidas: {contasVencidas.Count} (R$ {contasVencidas.Sum(c => c.ValorLiquido):N2})");
        if (topCategorias.Count > 0)
        {
            sb.AppendLine("Top categorias de despesa:");
            foreach (var cat in topCategorias)
                sb.AppendLine($"  - {cat.Categoria}: R$ {cat.Total:N2}");
        }

        return sb.ToString();
    }

    private static IReadOnlyList<AgenteInsight> ParseInsights(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];

        try
        {
            if (json.Contains("```"))
            {
                var lines = json.Split('\n');
                json = string.Join('\n', lines.Where(l => !l.StartsWith("```")));
            }

            var node = JsonNode.Parse(json);
            var array = node?["insights"]?.AsArray() ?? [];
            var result = new List<AgenteInsight>();

            foreach (var item in array)
            {
                if (item is null) continue;
                var tipo = item["tipo"]?.GetValue<string>() ?? "INFO";
                var mensagem = item["mensagem"]?.GetValue<string>();
                var valor = item["valor"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(mensagem))
                    result.Add(new AgenteInsight(tipo, mensagem, valor));
            }

            return result;
        }
        catch
        {
            return [];
        }
    }
}
