namespace ControleFinanceiro.Infrastructure.FinanceAI;

public sealed class OpenAiOptions
{
    public const string SectionName = "OpenAi";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Modelo Whisper para transcrição de áudio.</summary>
    public string WhisperModel { get; set; } = "whisper-1";

    /// <summary>Código de idioma BCP-47 enviado ao Whisper (ex: "pt").</summary>
    public string WhisperLanguage { get; set; } = "pt";
}
