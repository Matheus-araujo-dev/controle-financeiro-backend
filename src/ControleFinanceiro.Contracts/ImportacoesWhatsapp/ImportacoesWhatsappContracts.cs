using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Contracts.Filters;

namespace ControleFinanceiro.Contracts.ImportacoesWhatsapp;

public enum TipoOrigemImportacaoWhatsappRequest
{
    Texto = 1,
    Imagem = 2,
    Pdf = 3,
    Arquivo = 4
}

public sealed record ReceberImportacaoWhatsappWebhookRequest(
    TipoOrigemImportacaoWhatsappRequest TipoOrigem,
    string Remetente,
    string? TextoBruto,
    string? NomeArquivo,
    string? MimeType,
    string? ArquivoBase64);

public sealed record RevisarItemImportadoWhatsappRequest(string? Observacao);

public sealed record ImportacaoWhatsappListQueryRequest : ListQueryRequest
{
    public string? StatusCodigo { get; init; }
}

public sealed record ImportacaoWhatsappResumoResponse(
    Guid Id,
    string TipoOrigemCodigo,
    string TipoOrigemNome,
    string Remetente,
    string? TextoBruto,
    string? NomeArquivo,
    string? MimeType,
    string StatusCodigo,
    string StatusNome,
    decimal? ConfiancaExtracao,
    int QuantidadeItens,
    int QuantidadePendentes,
    DateTime RecebidoEmUtc,
    DateTime? ProcessadoEmUtc);

public sealed record ItemImportadoWhatsappResponse(
    Guid Id,
    Guid ImportacaoWhatsappId,
    string TipoSugestaoCodigo,
    string TipoSugestaoNome,
    string PayloadSugeridoJson,
    string StatusCodigo,
    string StatusNome,
    string? Observacao,
    DateTime? ConfirmadoEmUtc,
    DateTime? RejeitadoEmUtc);

public sealed record ImportacaoWhatsappDetalheResponse(
    Guid Id,
    string TipoOrigemCodigo,
    string TipoOrigemNome,
    string Remetente,
    string? TextoBruto,
    string? NomeArquivo,
    string? CaminhoArquivo,
    string? MimeType,
    string StatusCodigo,
    string StatusNome,
    decimal? ConfiancaExtracao,
    string? MensagemErro,
    DateTime RecebidoEmUtc,
    DateTime? ProcessadoEmUtc,
    DateTime? ConfirmadoEmUtc,
    DateTime? RejeitadoEmUtc,
    IReadOnlyCollection<ItemImportadoWhatsappResponse> Itens);
