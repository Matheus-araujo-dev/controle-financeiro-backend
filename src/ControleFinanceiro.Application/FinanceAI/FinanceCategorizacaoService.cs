using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Contracts.Agente;
using ControleFinanceiro.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json.Nodes;

namespace ControleFinanceiro.Application.FinanceAI;

public sealed class FinanceCategorizacaoService(
    ILlmClient llmClient,
    IAppDbContext db,
    ICurrentUser currentUser,
    ILogger<FinanceCategorizacaoService> logger)
{
    public async Task<IReadOnlyList<AgenteCategorizacaoItem>> CategorizarAsync(
        IReadOnlyList<string> descricoes, CancellationToken cancellationToken)
    {
        if (descricoes.Count == 0) return [];

        var familiaId = currentUser.FamiliaId
            ?? throw new InvalidOperationException("Família não identificada.");

        // Carrega categorias de despesa da família
        var categorias = await db.ContasGerenciais
            .AsNoTracking()
            .Where(c => c.FamiliaId == familiaId && c.Ativo
                     && !db.ContasGerenciais.Any(filho => filho.ContaPaiId == c.Id))
            .Select(c => new { c.Id, c.Descricao })
            .ToListAsync(cancellationToken);

        if (categorias.Count == 0)
            return descricoes.Select(d => new AgenteCategorizacaoItem(d, null, null, 0)).ToList();

        var categoriasJson = string.Join(", ", categorias.Select(c => $"\"{c.Descricao}\" (id:{c.Id})"));

        var exemploJson = """{"categorizacoes":[{"descricao":"texto","contaGerencialId":"guid-ou-null","contaGerencialDescricao":"texto-ou-null","confianca":0.0}]}""";
        var systemPrompt = $"""
            Você é um categorizador de transações financeiras brasileiras.
            Categorias disponíveis: [{categoriasJson}]

            Para cada descrição fornecida, retorne a categoria mais adequada e um nível de confiança (0.0 a 1.0).
            Se não tiver certeza razoável (confiança < 0.6), retorne null.

            Retorne SOMENTE um JSON (sem markdown):
            {exemploJson}
            """;

        var descricoesTexto = string.Join("\n", descricoes.Select((d, i) => $"{i + 1}. {d}"));
        var messages = new List<LlmMessage>
        {
            new(LlmRole.User, $"Categorize estas transações:\n{descricoesTexto}")
        };

        try
        {
            var request = new LlmRequest(LlmModelTier.Fast, systemPrompt, messages);
            var completion = await llmClient.CompleteAsync(request, cancellationToken);
            return ParseCategorizacoes(completion.Text, descricoes);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao categorizar transações via IA");
            return descricoes.Select(d => new AgenteCategorizacaoItem(d, null, null, 0)).ToList();
        }
    }

    private static IReadOnlyList<AgenteCategorizacaoItem> ParseCategorizacoes(
        string? json, IReadOnlyList<string> descricoes)
    {
        if (string.IsNullOrWhiteSpace(json))
            return descricoes.Select(d => new AgenteCategorizacaoItem(d, null, null, 0)).ToList();

        try
        {
            if (json.Contains("```"))
                json = string.Join('\n', json.Split('\n').Where(l => !l.StartsWith("```")));

            var node = JsonNode.Parse(json);
            var array = node?["categorizacoes"]?.AsArray() ?? [];
            var result = new List<AgenteCategorizacaoItem>();

            foreach (var item in array)
            {
                if (item is null) continue;
                var descricao = item["descricao"]?.GetValue<string>() ?? string.Empty;
                var idStr = item["contaGerencialId"]?.GetValue<string>();
                var nome = item["contaGerencialDescricao"]?.GetValue<string>();
                var confianca = item["confianca"]?.GetValue<double>() ?? 0;
                Guid? id = Guid.TryParse(idStr, out var g) ? g : null;
                result.Add(new AgenteCategorizacaoItem(descricao, id, nome, confianca));
            }

            return result;
        }
        catch
        {
            return descricoes.Select(d => new AgenteCategorizacaoItem(d, null, null, 0)).ToList();
        }
    }
}
