namespace ControleFinanceiro.Application.FinanceAI;

public interface ITranscricaoAudioService
{
    /// <summary>Transcreve áudio (base64) para texto. Retorna null em caso de falha.</summary>
    Task<string?> TranscreverAsync(string midiaBase64, string mimeType, CancellationToken cancellationToken);
}
