using System.Diagnostics;

namespace ControleFinanceiro.Infrastructure.ImportacoesWhatsapp;

internal static class InvoicePythonProcess
{
    public static async Task RunAsync(string executable, IReadOnlyList<string> arguments, string? input, TimeSpan limit, CancellationToken ct)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true, RedirectStandardInput = true, CreateNoWindow = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = start };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(limit);
        process.Start();
        var stderr = process.StandardError.ReadToEndAsync();
        var stdout = process.StandardOutput.ReadToEndAsync();
        try
        {
            if (input is not null) await process.StandardInput.WriteAsync(input.AsMemory(), timeout.Token);
            process.StandardInput.Close();
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            ct.ThrowIfCancellationRequested();
            throw new InvalidOperationException("A leitura do PDF demorou além do limite. Tente um arquivo menor.");
        }
        await Task.WhenAll(stderr, stdout);
        if (process.ExitCode != 0) throw new InvalidOperationException("Não foi possível processar o PDF. Verifique a qualidade do arquivo.");
    }
}
