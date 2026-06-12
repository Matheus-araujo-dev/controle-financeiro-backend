using System.Text;
using ControleFinanceiro.Application.ImportacoesWhatsapp;
using Microsoft.AspNetCore.Hosting;

namespace ControleFinanceiro.Infrastructure.ImportacoesWhatsapp;

public sealed class DefaultDocumentExtractor(IWebHostEnvironment environment) : IDocumentExtractor
{
    public async Task<DocumentExtractionResult> ExtractAsync(DocumentExtractionRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.TextoBruto))
        {
            return new DocumentExtractionResult(true, request.TextoBruto.Trim(), 0.94m, null);
        }

        if (string.IsNullOrWhiteSpace(request.CaminhoArquivo))
        {
            return new DocumentExtractionResult(false, null, null, "Não foi possível localizar conteúdo para extração.");
        }

        var absolutePath = ResolveAbsolutePath(request.CaminhoArquivo);
        if (!File.Exists(absolutePath))
        {
            return new DocumentExtractionResult(false, null, null, "Arquivo informado não foi encontrado para extração.");
        }

        if (request.MimeType?.Equals("text/plain", StringComparison.OrdinalIgnoreCase) == true)
        {
            return await ExtractPlainTextAsync(absolutePath, cancellationToken);
        }

        if (IsPdf(request))
        {
            return await ExtractPdfAsync(absolutePath, cancellationToken);
        }

        var description = request.MimeType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true
            ? $"Imagem recebida: {request.NomeArquivo}"
            : $"Documento recebido: {request.NomeArquivo}";

        return new DocumentExtractionResult(true, description, 0.78m, null);
    }

    private string ResolveAbsolutePath(string relativeOrAbsolutePath)
    {
        return Path.IsPathRooted(relativeOrAbsolutePath)
            ? relativeOrAbsolutePath
            : Path.Combine(environment.ContentRootPath, relativeOrAbsolutePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static bool IsPdf(DocumentExtractionRequest request)
    {
        return request.MimeType?.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) == true ||
               request.NomeArquivo?.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static async Task<DocumentExtractionResult> ExtractPlainTextAsync(string absolutePath, CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(absolutePath, cancellationToken);
        var text = Encoding.UTF8.GetString(bytes).Trim();
        return string.IsNullOrWhiteSpace(text)
            ? new DocumentExtractionResult(false, null, null, "Arquivo informado não possui texto legível.")
            : new DocumentExtractionResult(true, text, 0.90m, null);
    }

    private static async Task<DocumentExtractionResult> ExtractPdfAsync(string absolutePath, CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(absolutePath, cancellationToken);
        var tokens = PdfTextTokenizer.ExtractTokens(bytes);

        if (tokens.Count == 0)
        {
            return new DocumentExtractionResult(false, null, null, "Arquivo informado não possui texto legível.");
        }

        var normalizedText = CardInvoiceTextNormalizer.TryNormalize(tokens, out var text)
            ? text
            : string.Join(Environment.NewLine, tokens);

        return string.IsNullOrWhiteSpace(normalizedText)
            ? new DocumentExtractionResult(false, null, null, "Não foi possível extrair conteúdo útil do PDF informado.")
            : new DocumentExtractionResult(true, normalizedText, CardInvoiceTextNormalizer.IsInvoiceText(normalizedText) ? 0.93m : 0.88m, null);
    }
}
