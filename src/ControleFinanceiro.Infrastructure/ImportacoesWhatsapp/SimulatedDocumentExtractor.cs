using System.Text;
using ControleFinanceiro.Application.ImportacoesWhatsapp;
using Microsoft.AspNetCore.Hosting;

namespace ControleFinanceiro.Infrastructure.ImportacoesWhatsapp;

public sealed class SimulatedDocumentExtractor(IWebHostEnvironment environment) : IDocumentExtractor
{
    public async Task<DocumentExtractionResult> ExtractAsync(DocumentExtractionRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.TextoBruto))
        {
            return new DocumentExtractionResult(true, request.TextoBruto.Trim(), 0.94m, null);
        }

        if (string.IsNullOrWhiteSpace(request.CaminhoArquivo))
        {
            return new DocumentExtractionResult(false, null, null, "Nao foi possivel localizar conteudo para extracao.");
        }

        var absolutePath = Path.IsPathRooted(request.CaminhoArquivo)
            ? request.CaminhoArquivo
            : Path.Combine(environment.ContentRootPath, request.CaminhoArquivo.Replace('/', Path.DirectorySeparatorChar));

        if (request.MimeType?.Equals("text/plain", StringComparison.OrdinalIgnoreCase) == true && File.Exists(absolutePath))
        {
            var bytes = await File.ReadAllBytesAsync(absolutePath, cancellationToken);
            var text = Encoding.UTF8.GetString(bytes).Trim();
            return string.IsNullOrWhiteSpace(text)
                ? new DocumentExtractionResult(false, null, null, "Arquivo informado nao possui texto legivel.")
                : new DocumentExtractionResult(true, text, 0.90m, null);
        }

        var description = request.MimeType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true
            ? $"Imagem recebida: {request.NomeArquivo}"
            : $"Documento recebido: {request.NomeArquivo}";

        return new DocumentExtractionResult(true, description, 0.78m, null);
    }
}
