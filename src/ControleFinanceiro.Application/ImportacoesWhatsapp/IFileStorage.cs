namespace ControleFinanceiro.Application.ImportacoesWhatsapp;

public interface IFileStorage
{
    Task<FileStorageResult> SaveAsync(FileStorageRequest request, CancellationToken cancellationToken);
}

public sealed record FileStorageRequest(
    Guid ImportacaoId,
    string NomeArquivo,
    string MimeType,
    string ArquivoBase64);

public sealed record FileStorageResult(string CaminhoArquivo);
