using ControleFinanceiro.Application.ImportacoesWhatsapp;
using Microsoft.AspNetCore.Hosting;

namespace ControleFinanceiro.Infrastructure.ImportacoesWhatsapp;

public sealed class LocalImportFileStorage(IWebHostEnvironment environment) : IFileStorage
{
    private const int MaxFileBytes = 10 * 1024 * 1024;
    private static readonly char[] UnsafeFileNameChars =
        Path.GetInvalidFileNameChars()
            .Concat(['<', '>', ':', '"', '/', '\\', '|', '?', '*'])
            .Distinct()
            .ToArray();

    public async Task<FileStorageResult> SaveAsync(FileStorageRequest request, CancellationToken cancellationToken)
    {
        byte[] content;

        try
        {
            content = Convert.FromBase64String(request.ArquivoBase64);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("Arquivo base64 inválido.", nameof(request.ArquivoBase64), exception);
        }

        if (content.Length > MaxFileBytes)
        {
            throw new ArgumentException("Arquivo excede o limite de 10 MB.", nameof(request.ArquivoBase64));
        }

        var sanitizedName = SanitizeFileName(request.NomeArquivo);
        var relativePath = Path.Combine("App_Data", "importacoes-whatsapp", request.ImportacaoId.ToString("N"), sanitizedName);
        var absolutePath = Path.Combine(environment.ContentRootPath, relativePath);

        var directory = Path.GetDirectoryName(absolutePath)
            ?? throw new InvalidOperationException("Não foi possível determinar o diretório de armazenamento.");

        Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(absolutePath, content, cancellationToken);

        return new FileStorageResult(relativePath.Replace('\\', '/'));
    }

    private static string SanitizeFileName(string fileName)
    {
        return new string(fileName.Select(character => UnsafeFileNameChars.Contains(character) ? '_' : character).ToArray());
    }
}
