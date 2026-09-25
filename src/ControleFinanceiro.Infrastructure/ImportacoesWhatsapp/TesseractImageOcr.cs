using System.Diagnostics;
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
            var start = new ProcessStartInfo(options.Value.PythonExecutable) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true, CreateNoWindow = true };
            foreach (var argument in new[] { Path.Combine(AppContext.BaseDirectory, "Ocr", "invoice_image.py"), input, output, options.Value.TesseractExecutable })
                start.ArgumentList.Add(argument);
            using var process = new Process { StartInfo = start };
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(3));
            process.Start();
            var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
            var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException)
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
                cancellationToken.ThrowIfCancellationRequested();
                throw new InvalidOperationException("A leitura do PDF demorou além do limite. Tente um extrato menor.");
            }
            await Task.WhenAll(stderr, stdout);
            if (process.ExitCode != 0 || !File.Exists(output) || new FileInfo(output).Length > 1_000_000)
                throw new InvalidOperationException("Não foi possível reconhecer as imagens do PDF. Verifique a qualidade do extrato.");
            return await File.ReadAllTextAsync(output, cancellationToken);
        }
        finally { directory.Delete(recursive: true); }
    }
}
