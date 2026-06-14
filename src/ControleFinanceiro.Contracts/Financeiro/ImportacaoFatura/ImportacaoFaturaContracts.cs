namespace ControleFinanceiro.Contracts.Financeiro.ImportacaoFatura;

public sealed record ImportacaoFaturaItemPreview(
    DateOnly DataTransacao,
    string Descricao,
    decimal Valor,
    bool JaImportado,
    string ChaveImportacao);

public sealed record ImportacaoFaturaPreviewResponse(
    IReadOnlyCollection<ImportacaoFaturaItemPreview> Itens,
    decimal ValorTotal,
    int TotalItens,
    string? AvisoFormato);

public sealed record ImportacaoFaturaItemConfirmar(
    DateOnly DataTransacao,
    string Descricao,
    decimal Valor,
    string ChaveImportacao,
    Guid? ContaGerencialId = null);  // sobrescreve ContaGerencialPadraoId quando informado

public sealed record ConfirmarImportacaoFaturaRequest(
    Guid CartaoId,
    Guid FormaPagamentoId,
    Guid RecebedorPadraoId,
    Guid ContaGerencialPadraoId,
    IReadOnlyCollection<ImportacaoFaturaItemConfirmar> Itens);

public sealed record ConfirmarImportacaoFaturaResponse(
    int ContasCriadas,
    int ContasDuplicadas);
