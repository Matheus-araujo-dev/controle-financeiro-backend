namespace ControleFinanceiro.Application.FinanceAI;

public sealed class LlmOptions
{
    public const string SectionName = "FinanceAI";

    public string ApiKey { get; set; } = string.Empty;
    public string FastModel { get; set; } = "claude-haiku-4-5-20251001";
    public string ReasoningModel { get; set; } = "claude-sonnet-4-6";
    public int MaxTokens { get; set; } = 1024;
    public bool Enabled { get; set; } = false;
}
