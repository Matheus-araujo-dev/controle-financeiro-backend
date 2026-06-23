namespace ControleFinanceiro.Contracts.Anexos;

public sealed record AnexoResponse(
    Guid Id,
    string NomeArquivo,
    string MimeType,
    long TamanhoBytes,
    string Origem,
    DateTime CriadoEmUtc,
    string UrlConteudo);
