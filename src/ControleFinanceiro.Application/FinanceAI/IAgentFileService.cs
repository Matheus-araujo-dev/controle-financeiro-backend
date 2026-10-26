namespace ControleFinanceiro.Application.FinanceAI;

public interface IAgentFileService
{
    Task<AgentFileOutput> SalvarAsync(Guid familiaId, string nomeArquivo, string mimeType, byte[] conteudo, CancellationToken cancellationToken);
    Task<byte[]> LerAsync(string url, CancellationToken cancellationToken);
}
