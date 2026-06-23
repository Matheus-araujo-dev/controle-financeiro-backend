namespace ControleFinanceiro.Application.Anexos;

public interface IAnexoFileStorage
{
    Task<AnexoFileStorageResult> SaveAsync(Guid familiaId, Guid anexoId, string nomeArquivo, Stream conteudo, CancellationToken cancellationToken);
    Task<Stream> OpenReadAsync(string caminhoArquivo, CancellationToken cancellationToken);
    Task DeleteAsync(string caminhoArquivo, CancellationToken cancellationToken);
}

public sealed record AnexoFileStorageResult(string CaminhoArquivo, long TamanhoBytes, string HashSha256);
