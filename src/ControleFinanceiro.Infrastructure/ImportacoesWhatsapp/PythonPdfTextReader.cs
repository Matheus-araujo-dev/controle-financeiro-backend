using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ControleFinanceiro.Infrastructure.ImportacoesWhatsapp;

public sealed record InvoiceTextResult(string[][] Pages, string? Error);
public interface IInvoicePdfTextReader
{
    Task<InvoiceTextResult> ReadAsync(Stream stream, string? password, CancellationToken ct);
}

public sealed class PythonPdfTextReader(IOptions<InvoiceOcrOptions> options) : IInvoicePdfTextReader
{
    public async Task<InvoiceTextResult> ReadAsync(Stream stream, string? password, CancellationToken ct)
    {
        var directory = Directory.CreateTempSubdirectory("invoice-text-");
        try
        {
            var input = Path.Combine(directory.FullName, "input.pdf");
            var output = Path.Combine(directory.FullName, "result.json");
            await using (var file = File.Create(input)) await stream.CopyToAsync(file, ct);
            await InvoicePythonProcess.RunAsync(options.Value.PythonExecutable,
                [Path.Combine(AppContext.BaseDirectory, "Ocr", "pdf_text.py"), input, output],
                JsonSerializer.Serialize(password), TimeSpan.FromSeconds(45), ct);
            if (!File.Exists(output) || new FileInfo(output).Length > 4_000_000)
                throw new InvalidOperationException("O conteúdo do PDF excede o limite de leitura.");
            return JsonSerializer.Deserialize<InvoiceTextResult>(await File.ReadAllTextAsync(output, ct))
                ?? new([], "Não foi possível ler o PDF.");
        }
        finally { directory.Delete(recursive: true); }
    }
}
