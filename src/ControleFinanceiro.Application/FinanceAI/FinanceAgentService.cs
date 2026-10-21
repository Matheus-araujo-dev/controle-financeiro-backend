using System.Text.RegularExpressions;
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
    IAgentFileService fileService,
    ITranscricaoAudioService transcricaoService,
    ILogger<FinanceAgentService> logger) : IFinanceAgentService
{
    private const int MaxToolRounds = 15;
    private static readonly Regex ThinkTagRegex = new(@"<think>[\s\S]*?</think>\s*", RegexOptions.Compiled);

    public async Task<AgentResponse> ProcessarAsync(AgentRequest request, CancellationToken cancellationToken)
    {
        var familiaId = request.FamiliaId
            ?? currentUser.FamiliaId
            ?? throw new InvalidOperationException("Família não identificada.");

        db.DefinirFamiliaCorrente(familiaId);

        var (usuarioId, papel, nomeFamilia) = await ResolverContextoAsync(familiaId, request.UsuarioId, cancellationToken);

        // Transcrever áudio antes de processar, se for um arquivo de áudio
        var mensagemFinal = request.Mensagem;
        var arquivoAnexoPath = request.ArquivoAnexoPath;
        if (arquivoAnexoPath is not null)
        {
            var ext = Path.GetExtension(arquivoAnexoPath).ToLowerInvariant();
            var audioExts = new HashSet<string> { ".ogg", ".mp3", ".wav", ".m4a", ".aac", ".flac", ".wma", ".opus", ".webm" };
            if (audioExts.Contains(ext))
            {
                logger.LogDebug("Transcrevendo áudio {Path}", arquivoAnexoPath);
                var audioBytes = await File.ReadAllBytesAsync(arquivoAnexoPath, cancellationToken);
                var base64 = Convert.ToBase64String(audioBytes);
                var mime = ext switch
                {
                    ".ogg" or ".opus" => "audio/ogg",
                    ".mp3" => "audio/mpeg",
                    ".wav" => "audio/wav",
                    ".m4a" or ".aac" => "audio/mp4",
                    ".flac" => "audio/flac",
                    ".webm" => "audio/webm",
                    _ => "audio/ogg"
                };
                var transcricao = await transcricaoService.TranscreverAsync(base64, mime, cancellationToken);
                if (!string.IsNullOrWhiteSpace(transcricao))
                {
                    mensagemFinal = string.IsNullOrWhiteSpace(request.Mensagem)
                        ? $"[Áudio transcrito]: {transcricao}"
                        : $"{request.Mensagem}\n\n[Áudio transcrito]: {transcricao}";
                }
                arquivoAnexoPath = null;
            }
        }

        var toolContext = new ToolContext(familiaId, usuarioId, papel, nomeFamilia, arquivoAnexoPath, request.ArquivoAnexoNome, fileService);
        var toolMap = tools.ToDictionary(t => t.Name);
        var toolDefs = tools.Select(t => new LlmToolDefinition(t.Name, t.Description, t.InputSchema)).ToList();
        var systemPrompt = FinanceAgentSystemPrompt.Build(nomeFamilia, papel.ToString(), DateOnly.FromDateTime(DateTime.UtcNow));

        AiConversa conversa;
        bool isNewConversa;

        if (request.ConversaId.HasValue)
        {
            conversa = await db.AiConversas
                .Include(c => c.Mensagens)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.ConversaId.Value, cancellationToken)
                ?? AiConversa.Criar(familiaId, usuarioId, request.Canal, request.ContatoExterno);

            isNewConversa = conversa.Id != request.ConversaId.Value;
        }
        else
        {
            conversa = AiConversa.Criar(familiaId, usuarioId, request.Canal, request.ContatoExterno);
            isNewConversa = true;
        }

        conversa.AdicionarMensagem("user", mensagemFinal, request.ExternalMessageId);

        // Monta histórico de mensagens
        var messages = conversa.Mensagens
            .OrderBy(m => m.CreatedAtUtc)
            .Select(m => new LlmMessage(
                m.Papel == "user" ? LlmRole.User : LlmRole.Assistant,
                m.Conteudo))
            .ToList();

        // Vision: se a mensagem atual tem imagem anexada, inclui no último LlmMessage (user)
        if (request.ArquivoAnexoPath is not null)
        {
            var ext = Path.GetExtension(request.ArquivoAnexoPath).ToLowerInvariant();
            var imageMimes = new HashSet<string> { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };
            if (imageMimes.Contains(ext) && File.Exists(request.ArquivoAnexoPath))
            {
                var imageBytes = await File.ReadAllBytesAsync(request.ArquivoAnexoPath, cancellationToken);
                var base64 = Convert.ToBase64String(imageBytes);
                var mime = ext switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".gif" => "image/gif",
                    ".webp" => "image/webp",
                    ".bmp" => "image/bmp",
                    _ => "image/jpeg"
                };

                if (messages.Count > 0)
                {
                    var last = messages[^1];
                    messages[^1] = last with { ImageBase64 = base64, ImageMimeType = mime };
                }
            }
        }

        var totalTokens = 0;
        var resposta = string.Empty;
        var toolCallsData = new List<(string Nome, string Input, string Output, string Status, int InTokens, int OutTokens)>();

        // Loop de function calling
        for (var round = 0; round < MaxToolRounds; round++)
        {
            var tier = messages.Any(m => m.ImageBase64 is not null) ? LlmModelTier.Reasoning : LlmModelTier.Fast;
            var llmRequest = new LlmRequest(tier, systemPrompt, messages, toolDefs);
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

                toolCallsData.Add((toolCall.ToolName, toolCall.InputJson, toolOutput, "ok",
                    completion.Usage.InputTokens, completion.Usage.OutputTokens));

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

        resposta = ThinkTagRegex.Replace(resposta, string.Empty).Trim();

        // Persist conversation data
        await SalvarConversaAsync(conversa, isNewConversa, mensagemFinal, request.ExternalMessageId, resposta, toolCallsData, cancellationToken);

        return new AgentResponse(resposta, conversa.Id, totalTokens,
            toolContext.ArquivosGerados.Count > 0 ? toolContext.ArquivosGerados : null);
    }

    private async Task SalvarConversaAsync(
        AiConversa conversa,
        bool isNewConversa,
        string mensagem,
        string? externalMessageId,
        string resposta,
        List<(string Nome, string Input, string Output, string Status, int InTokens, int OutTokens)> toolCallsData,
        CancellationToken cancellationToken)
    {
        try
        {
            if (isNewConversa)
            {
                conversa.AdicionarMensagem("assistant", resposta);
                db.AiConversas.Add(conversa);
            }
            else
            {
                db.AiMensagens.Add(AiMensagem.Criar(conversa.Id, "user", mensagem, externalMessageId));
                db.AiMensagens.Add(AiMensagem.Criar(conversa.Id, "assistant", resposta));
                foreach (var tc in toolCallsData)
                    db.AiToolCalls.Add(AiToolCall.Criar(conversa.Id, tc.Nome, tc.Input, tc.Output, tc.Status, tc.InTokens, tc.OutTokens));
            }

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex,
                "Concurrency ao salvar conversa {ConversaId}. Entries: [{Entries}]",
                conversa.Id,
                string.Join(", ", ex.Entries.Select(e => $"{e.Entity.GetType().Name}[{e.State}]")));

            try
            {
                db.LimparChangeTracker();
                db.AiMensagens.Add(AiMensagem.Criar(conversa.Id, "user", mensagem, externalMessageId));
                db.AiMensagens.Add(AiMensagem.Criar(conversa.Id, "assistant", resposta));
                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Retry salvou mensagens da conversa {ConversaId}", conversa.Id);
            }
            catch (Exception retryEx)
            {
                logger.LogError(retryEx,
                    "Retry falhou ao salvar conversa {ConversaId}. Tipo: {ExType}",
                    conversa.Id, retryEx.GetType().Name);
            }
        }
    }

    public async Task<AgentResponse> ProcessarStreamAsync(AgentRequest request, Func<AgentStreamEvent, Task> onEvent, CancellationToken cancellationToken)
    {
        await onEvent(new AgentStreamEvent(AgentStreamEventType.Processando, "Iniciando processamento..."));

        var familiaId = request.FamiliaId
            ?? currentUser.FamiliaId
            ?? throw new InvalidOperationException("Família não identificada.");

        db.DefinirFamiliaCorrente(familiaId);

        var (usuarioId, papel, nomeFamilia) = await ResolverContextoAsync(familiaId, request.UsuarioId, cancellationToken);

        var mensagemFinal = request.Mensagem;
        var arquivoAnexoPath = request.ArquivoAnexoPath;
        if (arquivoAnexoPath is not null)
        {
            var ext = Path.GetExtension(arquivoAnexoPath).ToLowerInvariant();
            var audioExts = new HashSet<string> { ".ogg", ".mp3", ".wav", ".m4a", ".aac", ".flac", ".wma", ".opus", ".webm" };
            if (audioExts.Contains(ext))
            {
                await onEvent(new AgentStreamEvent(AgentStreamEventType.Processando, "Transcrevendo áudio..."));
                var audioBytes = await File.ReadAllBytesAsync(arquivoAnexoPath, cancellationToken);
                var base64 = Convert.ToBase64String(audioBytes);
                var mime = ext switch
                {
                    ".ogg" or ".opus" => "audio/ogg",
                    ".mp3" => "audio/mpeg",
                    ".wav" => "audio/wav",
                    ".m4a" or ".aac" => "audio/mp4",
                    ".flac" => "audio/flac",
                    ".webm" => "audio/webm",
                    _ => "audio/ogg"
                };
                var transcricao = await transcricaoService.TranscreverAsync(base64, mime, cancellationToken);
                if (!string.IsNullOrWhiteSpace(transcricao))
                {
                    mensagemFinal = string.IsNullOrWhiteSpace(request.Mensagem)
                        ? $"[Áudio transcrito]: {transcricao}"
                        : $"{request.Mensagem}\n\n[Áudio transcrito]: {transcricao}";
                }
                arquivoAnexoPath = null;
            }
        }

        var toolContext = new ToolContext(familiaId, usuarioId, papel, nomeFamilia, arquivoAnexoPath, request.ArquivoAnexoNome, fileService);
        var toolMap = tools.ToDictionary(t => t.Name);
        var toolDefs = tools.Select(t => new LlmToolDefinition(t.Name, t.Description, t.InputSchema)).ToList();
        var systemPrompt = FinanceAgentSystemPrompt.Build(nomeFamilia, papel.ToString(), DateOnly.FromDateTime(DateTime.UtcNow));

        AiConversa conversa;
        bool isNewConversa;

        if (request.ConversaId.HasValue)
        {
            conversa = await db.AiConversas
                .Include(c => c.Mensagens)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.ConversaId.Value, cancellationToken)
                ?? AiConversa.Criar(familiaId, usuarioId, request.Canal, request.ContatoExterno);

            isNewConversa = conversa.Id != request.ConversaId.Value;
        }
        else
        {
            conversa = AiConversa.Criar(familiaId, usuarioId, request.Canal, request.ContatoExterno);
            isNewConversa = true;
        }

        conversa.AdicionarMensagem("user", mensagemFinal, request.ExternalMessageId);

        var messages = conversa.Mensagens
            .OrderBy(m => m.CreatedAtUtc)
            .Select(m => new LlmMessage(
                m.Papel == "user" ? LlmRole.User : LlmRole.Assistant,
                m.Conteudo))
            .ToList();

        if (request.ArquivoAnexoPath is not null)
        {
            var ext = Path.GetExtension(request.ArquivoAnexoPath).ToLowerInvariant();
            var imageMimes = new HashSet<string> { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };
            if (imageMimes.Contains(ext) && File.Exists(request.ArquivoAnexoPath))
            {
                var imageBytes = await File.ReadAllBytesAsync(request.ArquivoAnexoPath, cancellationToken);
                var base64 = Convert.ToBase64String(imageBytes);
                var mime = ext switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".gif" => "image/gif",
                    ".webp" => "image/webp",
                    ".bmp" => "image/bmp",
                    _ => "image/jpeg"
                };

                if (messages.Count > 0)
                {
                    var last = messages[^1];
                    messages[^1] = last with { ImageBase64 = base64, ImageMimeType = mime };
                }
            }
        }

        var totalTokens = 0;
        var resposta = string.Empty;
        var toolCallsData = new List<(string Nome, string Input, string Output, string Status, int InTokens, int OutTokens)>();

        await onEvent(new AgentStreamEvent(AgentStreamEventType.Processando, "Consultando IA..."));

        for (var round = 0; round < MaxToolRounds; round++)
        {
            var tier = messages.Any(m => m.ImageBase64 is not null) ? LlmModelTier.Reasoning : LlmModelTier.Fast;
            var llmRequest = new LlmRequest(tier, systemPrompt, messages, toolDefs);
            var completion = await llmClient.CompleteAsync(llmRequest, cancellationToken);
            totalTokens += completion.Usage.InputTokens + completion.Usage.OutputTokens;

            if (completion.ToolCalls.Count == 0 || completion.StopReason == "end_turn")
            {
                resposta = completion.Text ?? "Não consegui processar sua solicitação.";
                await onEvent(new AgentStreamEvent(AgentStreamEventType.Texto, resposta));
                break;
            }

            var toolResultsContent = new System.Text.StringBuilder();
            foreach (var toolCall in completion.ToolCalls)
            {
                await onEvent(new AgentStreamEvent(AgentStreamEventType.Ferramenta, toolCall.ToolName));

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

                toolCallsData.Add((toolCall.ToolName, toolCall.InputJson, toolOutput, "ok",
                    completion.Usage.InputTokens, completion.Usage.OutputTokens));

                toolResultsContent.AppendLine($"[{toolCall.ToolName}]: {toolOutput}");
            }

            messages.Add(new LlmMessage(LlmRole.Assistant,
                $"Chamando ferramentas: {string.Join(", ", completion.ToolCalls.Select(t => t.ToolName))}"));
            messages.Add(new LlmMessage(LlmRole.User,
                $"Resultados das ferramentas:\n{toolResultsContent}"));
        }

        if (string.IsNullOrEmpty(resposta))
            resposta = "Não consegui concluir a consulta. Tente novamente.";

        resposta = ThinkTagRegex.Replace(resposta, string.Empty).Trim();

        // Report generated files
        foreach (var arquivo in toolContext.ArquivosGerados)
            await onEvent(new AgentStreamEvent(AgentStreamEventType.Arquivo, Arquivo: arquivo));

        await SalvarConversaAsync(conversa, isNewConversa, mensagemFinal, request.ExternalMessageId, resposta, toolCallsData, cancellationToken);

        var result = new AgentResponse(resposta, conversa.Id, totalTokens,
            toolContext.ArquivosGerados.Count > 0 ? toolContext.ArquivosGerados : null);

        await onEvent(new AgentStreamEvent(AgentStreamEventType.Concluido));

        return result;
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
