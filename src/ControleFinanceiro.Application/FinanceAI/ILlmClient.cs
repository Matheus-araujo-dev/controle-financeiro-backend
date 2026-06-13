namespace ControleFinanceiro.Application.FinanceAI;

public interface ILlmClient
{
    Task<LlmCompletion> CompleteAsync(LlmRequest request, CancellationToken cancellationToken);
}

public sealed record LlmRequest(
    LlmModelTier Tier,
    string SystemPrompt,
    IReadOnlyList<LlmMessage> Messages,
    IReadOnlyList<LlmToolDefinition>? Tools = null,
    string? ResponseFormatSchema = null);

public sealed record LlmMessage(LlmRole Role, string Content);

public sealed record LlmToolDefinition(
    string Name,
    string Description,
    string InputSchema);

public sealed record LlmCompletion(
    string? Text,
    IReadOnlyList<LlmToolCall> ToolCalls,
    LlmUsage Usage,
    string? StopReason);

public sealed record LlmToolCall(string ToolName, string InputJson, string ToolUseId);

public sealed record LlmUsage(int InputTokens, int OutputTokens);

public enum LlmModelTier
{
    Fast = 1,      // Haiku — categorização, perguntas simples
    Reasoning = 2  // Sonnet — análise, Vision, insights
}

public enum LlmRole
{
    User = 1,
    Assistant = 2,
    ToolResult = 3
}

public interface ILlmVisionClient
{
    /// <summary>Analisa uma imagem via Claude Vision e retorna o texto da resposta.</summary>
    Task<string?> AnalisarImagemAsync(
        string systemPrompt,
        string userText,
        string imagemBase64,
        string mimeType,
        CancellationToken cancellationToken);
}
