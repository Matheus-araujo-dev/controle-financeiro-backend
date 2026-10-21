using ControleFinanceiro.Application.FinanceAI;

namespace ControleFinanceiro.Infrastructure.FinanceAI;

public sealed class LocalAgentFileService : IAgentFileService
{
    private static readonly string BasePath = Path.Combine("App_Data", "agente-gerados");

    public async Task<AgentFileOutput> SalvarAsync(Guid familiaId, string nomeArquivo, string mimeType, byte[] conteudo, CancellationToken cancellationToken)
    {
        var fileId = Guid.NewGuid();
        var dir = Path.Combine(BasePath, familiaId.ToString(), fileId.ToString());
        Directory.CreateDirectory(dir);

        var filePath = Path.Combine(dir, nomeArquivo);
        await File.WriteAllBytesAsync(filePath, conteudo, cancellationToken);

        var url = $"/api/v1/agente/arquivos/{fileId}/{nomeArquivo}";
        return new AgentFileOutput(nomeArquivo, mimeType, url, conteudo.Length);
    }

    public async Task<byte[]> LerAsync(string url, CancellationToken cancellationToken)
    {
        var parts = url.Split('/');
        if (parts.Length < 6)
            throw new FileNotFoundException("URL de arquivo inválida.");

        var fileId = parts[^2];
        var nomeArquivo = parts[^1];

        var files = Directory.GetFiles(BasePath, nomeArquivo, SearchOption.AllDirectories)
            .Where(f => f.Contains(fileId))
            .ToArray();

        if (files.Length == 0)
            throw new FileNotFoundException("Arquivo não encontrado.");

        return await File.ReadAllBytesAsync(files[0], cancellationToken);
    }
}
