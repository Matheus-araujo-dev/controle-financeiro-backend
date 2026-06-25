using System.Security.Cryptography;
using ControleFinanceiro.Application.Anexos;
using Microsoft.AspNetCore.Hosting;

namespace ControleFinanceiro.Infrastructure.Anexos;

public sealed class LocalAnexoFileStorage(IWebHostEnvironment environment) : IAnexoFileStorage
{
    private const int MaxFileBytes = 10 * 1024 * 1024;
    private static readonly char[] UnsafeFileNameChars = Path.GetInvalidFileNameChars()
        .Concat(['<', '>', ':', '"', '/', '\\', '|', '?', '*']).Distinct().ToArray();

    public async Task<AnexoFileStorageResult> SaveAsync(
        Guid familiaId,
        Guid anexoId,
        string nomeArquivo,
        Stream conteudo,
        CancellationToken cancellationToken)
    {
        var relativePath = Path.Combine(
            "App_Data", "anexos", familiaId.ToString("N"), anexoId.ToString("N"), SanitizeFileName(nomeArquivo));
        var absolutePath = ResolveSafePath(relativePath);
        var directory = Path.GetDirectoryName(absolutePath)
            ?? throw new InvalidOperationException("Não foi possível determinar o diretório de armazenamento.");
        Directory.CreateDirectory(directory);

        try
        {
            await using var output = new FileStream(absolutePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
            using var sha256 = SHA256.Create();
            var buffer = new byte[81920];
            long total = 0;

            while (true)
            {
                var read = await conteudo.ReadAsync(buffer, cancellationToken);
                if (read == 0) break;
                total += read;
                if (total > MaxFileBytes) throw new ArgumentException("Arquivo excede o limite de 10 MB.", nameof(conteudo));

                sha256.TransformBlock(buffer, 0, read, null, 0);
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            if (total == 0) throw new ArgumentException("Arquivo vazio não é permitido.", nameof(conteudo));
            sha256.TransformFinalBlock([], 0, 0);

            return new AnexoFileStorageResult(
                relativePath.Replace('\\', '/'),
                total,
                Convert.ToHexString(sha256.Hash!).ToLowerInvariant());
        }
        catch
        {
            if (File.Exists(absolutePath)) File.Delete(absolutePath);
            throw;
        }
    }

    public Task<Stream> OpenReadAsync(string caminhoArquivo, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var absolutePath = ResolveSafePath(caminhoArquivo);
        if (!File.Exists(absolutePath)) throw new FileNotFoundException("Arquivo do anexo não foi encontrado.", absolutePath);
        return Task.FromResult<Stream>(new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true));
    }

    public Task DeleteAsync(string caminhoArquivo, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var absolutePath = ResolveSafePath(caminhoArquivo);
        if (File.Exists(absolutePath)) File.Delete(absolutePath);

        var directory = Path.GetDirectoryName(absolutePath);
        if (directory is not null && Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
        {
            Directory.Delete(directory);
        }
        return Task.CompletedTask;
    }

    private string ResolveSafePath(string relativePath)
    {
        var root = Path.GetFullPath(environment.ContentRootPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var absolute = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!absolute.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Caminho de anexo inválido.");
        return absolute;
    }

    private static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName.Trim());
        var sanitized = new string(name.Select(character => UnsafeFileNameChars.Contains(character) ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "anexo" : sanitized;
    }
}
