using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.FinanceAI.Tools;
using ControleFinanceiro.Domain.FinanceAI;
using ControleFinanceiro.Domain.Identidade;
using ControleFinanceiro.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ControleFinanceiro.Application.FinanceAI;

public sealed class FinanceAgentService(
    ILlmClient llmClient,
    IAppDbContext db,
    ICurrentUser currentUser,
    IEnumerable<IFinanceTool> tools,
    ILogger<FinanceAgentService> logger) : IFinanceAgentService
{
    private const int MaxToolRounds = 5;

    public async Task<AgentResponse> ProcessarAsync(AgentRequest request, CancellationToken cancellationToken)
    {
        var familiaId = request.FamiliaId
            ?? currentUser.FamiliaId
            ?? throw new InvalidOperationException("Família não identificada.");

        var (usuarioId, papel, nomeFamilia) = await ResolverContextoAsync(familiaId, request.UsuarioId, cancellationToken);

        var toolContext = new ToolContext(familiaId, usuarioId, papel, nomeFamilia);
        var toolMap = tools.ToDictionary(t => t.Name);
        var toolDefs = tools.Select(t => new LlmToolDefinition(t.Name, t.Description, t.InputSchema)).ToList();
        var systemPrompt = FinanceAgentSystemPrompt.Build(nomeFamilia, papel.ToString(), DateOnly.FromDateTime(DateTime.UtcNow));

        // Carrega ou cria conversa
        AiConversa conversa;
        if (request.ConversaId.HasValue)
        {
            conversa = await db.AiConversas
                .Include(c => c.Mensagens)
                .FirstOrDefaultAsync(c => c.Id == request.ConversaId.Value, cancellationToken)
                ?? AiConversa.Criar(familiaId, usuarioId, request.Canal, request.ContatoExterno);
        }
        else
        {
            conversa = AiConversa.Criar(familiaId, usuarioId, request.Canal, request.ContatoExterno);
            db.AiConversas.Add(conversa);
        }

        conversa.AdicionarMensagem("user", request.Mensagem, request.ExternalMessageId);

        // Monta histórico de mensagens
        var messages = conversa.Mensagens
            .OrderBy(m => m.CreatedAtUtc)
            .Select(m => new LlmMessage(
                m.Papel == "user" ? LlmRole.User : LlmRole.Assistant,
                m.Conteudo))
            .ToList();

        var totalTokens = 0;
        var resposta = string.Empty;

        // Loop de function calling
        for (var round = 0; round < MaxToolRounds; round++)
        {
            var llmRequest = new LlmRequest(LlmModelTier.Fast, systemPrompt, messages, toolDefs);
            var completion = await llmClient.CompleteAsync(llmRequest, cancellationToken);
            totalTokens += completion.Usage.InputTokens + completion.Usage.OutputTokens;

            if (completion.ToolCalls.Count == 0 || completion.StopReason == "end_turn")
            {
                resposta = completion.Text ?? "Não consegui processar sua solicitação.";
                break;
            }

            // Executa ferramentas e adiciona resultados ao histórico
            var toolResultsContent = new System.Text.StringBuilder();
            foreach (var toolCall in completion.ToolCalls)
            {
                logger.LogDebug("Executando ferramenta {Tool}", toolCall.ToolName);

                string toolOutput;
                if (toolMap.TryGetValue(toolCall.ToolName, out var tool))
                {
                    try
                    {
                        toolOutput = await tool.ExecuteAsync(toolCall.InputJson, toolContext, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Erro ao executar ferramenta {Tool}", toolCall.ToolName);
                        toolOutput = """{"erro":"Erro ao executar a ferramenta. Tente novamente."}""";
                    }
                }
                else
                {
                    toolOutput = """{"erro":"Ferramenta não encontrada."}""";
                }

                conversa.RegistrarToolCall(
                    toolCall.ToolName, toolCall.InputJson, toolOutput, "ok",
                    completion.Usage.InputTokens, completion.Usage.OutputTokens);

                toolResultsContent.AppendLine($"[{toolCall.ToolName}]: {toolOutput}");
            }

            // Adiciona o turno do assistente (tool_use) e o resultado ao histórico
            messages.Add(new LlmMessage(LlmRole.Assistant,
                $"Chamando ferramentas: {string.Join(", ", completion.ToolCalls.Select(t => t.ToolName))}"));
            messages.Add(new LlmMessage(LlmRole.User,
                $"Resultados das ferramentas:\n{toolResultsContent}"));
        }

        if (string.IsNullOrEmpty(resposta))
            resposta = "Não consegui concluir a consulta. Tente novamente.";

        conversa.AdicionarMensagem("assistant", resposta);
        await db.SaveChangesAsync(cancellationToken);

        return new AgentResponse(resposta, conversa.Id, totalTokens);
    }

    private async Task<(Guid UsuarioId, PapelFamilia Papel, string NomeFamilia)> ResolverContextoAsync(
        Guid familiaId, Guid? usuarioIdOverride, CancellationToken ct)
    {
        // JWT sub = usuario.Id (GUID) — ver JwtTokenService
        Guid.TryParse(currentUser.UserId, out var usuarioIdHttp);
        var usuarioId = usuarioIdOverride ?? usuarioIdHttp;

        var membro = usuarioId != Guid.Empty
            ? await db.MembrosFamilia
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.FamiliaId == familiaId && m.UsuarioId == usuarioId, ct)
            : null;

        var papel = membro?.Papel ?? PapelFamilia.Membro;

        var nomeFamilia = await db.Familias
            .AsNoTracking()
            .Where(f => f.Id == familiaId)
            .Select(f => f.Nome)
            .FirstOrDefaultAsync(ct) ?? "Família";

        return (usuarioId, papel, nomeFamilia);
    }
}
