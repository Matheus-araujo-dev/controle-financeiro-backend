namespace ControleFinanceiro.Application.FinanceAI;

public sealed record ExtracaoImagemResultado(
    bool Sucesso,
    string? Estabelecimento,
    decimal? Valor,
    DateOnly? Data,
    string? Descricao);

public interface IExtracaoImagemFinanceiroService
{
    Task<ExtracaoImagemResultado> ExtrairAsync(string imagemBase64, string mimeType, CancellationToken cancellationToken);
}
