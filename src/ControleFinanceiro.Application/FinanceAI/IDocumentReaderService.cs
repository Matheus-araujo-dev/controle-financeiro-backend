namespace ControleFinanceiro.Application.FinanceAI;

public interface IDocumentReaderService
{
    Task<DocumentReadResult> LerAsync(string caminhoArquivo, string? nomeArquivo, CancellationToken cancellationToken);
}

public sealed record DocumentReadResult(
    bool Sucesso,
    string? Conteudo,
    string? FormatoDetectado,
    int TotalLinhas,
    string? MensagemErro = null);
