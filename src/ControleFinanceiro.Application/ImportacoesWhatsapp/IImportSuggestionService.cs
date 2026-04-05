using ControleFinanceiro.Domain.ImportacoesWhatsapp;

namespace ControleFinanceiro.Application.ImportacoesWhatsapp;

public interface IImportSuggestionService
{
    Task<IReadOnlyCollection<ImportSuggestionItem>> GenerateAsync(
        ImportSuggestionRequest request,
        CancellationToken cancellationToken);
}

public sealed record ImportSuggestionRequest(
    TipoOrigemImportacaoWhatsapp TipoOrigem,
    string Remetente,
    string TextoExtraido,
    string? NomeArquivo,
    string? MimeType);

public sealed record ImportSuggestionItem(
    TipoSugestaoImportacaoWhatsapp TipoSugestao,
    string PayloadSugeridoJson);
