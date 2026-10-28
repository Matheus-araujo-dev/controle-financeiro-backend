namespace ControleFinanceiro.Application.ImportacoesWhatsapp;

public sealed record HistoricalPredictionData(
    Guid? ContaGerencialId,
    Guid? PagadorId,
    string? DescricaoAjustada,
    bool GerarContaReceber,
    bool MarcarComoRecorrente,
    int QuantidadeOcorrencias,
    decimal ConfiancaHistorico);

public sealed record ContaGerencialHeuristicaData(
    Guid Id,
    string? Codigo,
    string Descricao,
    Guid? MembroPadraoId,
    Guid? ContaPaiId);

public sealed record CardPurchaseForecastStatusData(
    string StatusCodigo,
    string StatusNome);