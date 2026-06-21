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

public sealed record RevisarItemImportadoWhatsappRequest(
    string? Observacao,
    string? DescricaoAjustada,
    Guid? ContaGerencialId,
    Guid? ResponsavelId,
    DateOnly? DataVencimentoContaReceber,
    bool GerarContaReceber,
    bool MarcarComoRecorrente);

public sealed record AprovarImportacaoWhatsappRequest(
    Guid? RecebedorFaturaId,
    Guid? ResponsavelPagamentoFaturaId,
    IReadOnlyCollection<Guid>? CartaoIds);

public sealed record ImportacaoWhatsappListQueryRequest : ListQueryRequest
{
    public string? TipoOrigemCodigo { get; init; }

    public string? StatusCodigo { get; init; }

    public string? Remetente { get; init; }

    public string? NomeArquivo { get; init; }

    public string? MimeType { get; init; }

    public decimal? ConfiancaExtracaoMin { get; init; }

    public decimal? ConfiancaExtracaoMax { get; init; }

    public DateOnly? RecebidoEmInicial { get; init; }

    public DateOnly? RecebidoEmFinal { get; init; }

    public DateOnly? ProcessadoEmInicial { get; init; }

    public DateOnly? ProcessadoEmFinal { get; init; }
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
    string? DescricaoAjustada,
    bool MarcarComoRecorrente,
    Guid? ContaGerencialId,
    string? ContaGerencialDescricao,
    Guid? ResponsavelId,
    string? ResponsavelNome,
    Guid? ContaReceberId,
    string? StatusPrevisaoCodigo,
    string? StatusPrevisaoNome,
    string? Observacao,
    DateTime? ConfirmadoEmUtc,
    DateTime? RejeitadoEmUtc,
    PredicaoClassificacaoImportacaoWhatsappResponse? Predicao);

public sealed record PredicaoClassificacaoImportacaoWhatsappResponse(
    Guid? ContaGerencialId,
    string? ContaGerencialDescricao,
    Guid? ResponsavelId,
    string? ResponsavelNome,
    string? DescricaoAjustada,
    bool GerarContaReceber,
    bool MarcarComoRecorrente,
    int QuantidadeOcorrencias,
    decimal ConfiancaHistorico);

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
    bool PossuiGeracaoFinanceira,
    IReadOnlyCollection<ItemImportadoWhatsappResponse> Itens);
