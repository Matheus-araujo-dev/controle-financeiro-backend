namespace ControleFinanceiro.Application.ImportacoesWhatsapp;

public interface IDocumentExtractor
{
    Task<DocumentExtractionResult> ExtractAsync(DocumentExtractionRequest request, CancellationToken cancellationToken);
}

public sealed record DocumentExtractionRequest(
    string? TextoBruto,
    string? NomeArquivo,
    string? MimeType,
    string? CaminhoArquivo);

public sealed record DocumentExtractionResult(
    bool Success,
    string? TextoExtraido,
    decimal? Confianca,
    string? MensagemErro);
