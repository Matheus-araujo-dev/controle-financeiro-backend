using ControleFinanceiro.Application.ImportacoesWhatsapp;
using Microsoft.AspNetCore.Hosting;

namespace ControleFinanceiro.Infrastructure.ImportacoesWhatsapp;

public sealed class LocalImportFileStorage(IWebHostEnvironment environment) : IFileStorage
{
    public async Task<FileStorageResult> SaveAsync(FileStorageRequest request, CancellationToken cancellationToken)
    {
        byte[] content;

        try
        {
            content = Convert.FromBase64String(request.ArquivoBase64);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("Arquivo base64 invalido.", nameof(request.ArquivoBase64), exception);
        }

        var sanitizedName = SanitizeFileName(request.NomeArquivo);
        var relativePath = Path.Combine("App_Data", "importacoes-whatsapp", request.ImportacaoId.ToString("N"), sanitizedName);
        var absolutePath = Path.Combine(environment.ContentRootPath, relativePath);

        var directory = Path.GetDirectoryName(absolutePath)
            ?? throw new InvalidOperationException("Nao foi possivel determinar o diretorio de armazenamento.");

        Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(absolutePath, content, cancellationToken);

        return new FileStorageResult(relativePath.Replace('\\', '/'));
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return new string(fileName.Select(character => invalidChars.Contains(character) ? '_' : character).ToArray());
    }
}
