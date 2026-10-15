namespace ControleFinanceiro.Application.FinanceAI;

public sealed class LlmOptions
{
    public const string SectionName = "FinanceAI";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Provider da LLM: "Anthropic" ou "OpenAiCompatible" (MiniMax, OpenAI, Groq, etc.).
    /// </summary>
    public string Provider { get; set; } = "Anthropic";

    /// <summary>
    /// Base URL da API. Usado apenas quando Provider = OpenAiCompatible.
    /// Exemplos: https://api.minimax.chat/v1, https://api.openai.com/v1
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    public string FastModel { get; set; } = "claude-haiku-4-5-20251001";
    public string ReasoningModel { get; set; } = "claude-sonnet-4-6";
    public int MaxTokens { get; set; } = 1024;
    public bool Enabled { get; set; } = false;

    public bool IsOpenAiCompatible =>
        string.Equals(Provider, "OpenAiCompatible", StringComparison.OrdinalIgnoreCase);
}
