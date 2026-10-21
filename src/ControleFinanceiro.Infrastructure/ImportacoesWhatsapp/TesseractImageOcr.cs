using Microsoft.Extensions.Options;

namespace ControleFinanceiro.Infrastructure.ImportacoesWhatsapp;

public interface IInvoiceImageOcr
{
    Task<string> ReadAsync(byte[] png, CancellationToken cancellationToken);
}

public sealed class InvoiceOcrOptions
{
    public string PythonExecutable { get; set; } = "python3";
    public string TesseractExecutable { get; set; } = "tesseract";
}

public sealed class TesseractImageOcr(IOptions<InvoiceOcrOptions> options) : IInvoiceImageOcr
{
    public async Task<string> ReadAsync(byte[] png, CancellationToken cancellationToken)
    {
        var directory = Directory.CreateTempSubdirectory("invoice-ocr-");
        try
        {
            var input = Path.Combine(directory.FullName, "image.png");
            var output = Path.Combine(directory.FullName, "text.txt");
            await File.WriteAllBytesAsync(input, png, cancellationToken);
            await InvoicePythonProcess.RunAsync(options.Value.PythonExecutable,
                [Path.Combine(AppContext.BaseDirectory, "Ocr", "invoice_image.py"), input, output, options.Value.TesseractExecutable],
                null, TimeSpan.FromMinutes(3), cancellationToken);
            if (!File.Exists(output) || new FileInfo(output).Length > 1_000_000)
                throw new InvalidOperationException("Não foi possível reconhecer as imagens do PDF. Verifique a qualidade do extrato.");
            return await File.ReadAllTextAsync(output, cancellationToken);
        }
        finally { directory.Delete(recursive: true); }
    }
}
