namespace ControleFinanceiro.Application.ImportacoesWhatsapp;

public sealed record HistoricalPredictionData(
    Guid? ContaGerencialId,
    Guid? ResponsavelId,
    string? DescricaoAjustada,
    bool GerarContaReceber,
    bool MarcarComoRecorrente,
    int QuantidadeOcorrencias,
    decimal ConfiancaHistorico);

public sealed record ContaGerencialHeuristicaData(
    Guid Id,
    string? Codigo,
    string Descricao,
    Guid? ResponsavelPadraoId,
    Guid? ContaPaiId);

public sealed record CardPurchaseForecastStatusData(
    string StatusCodigo,
    string StatusNome);