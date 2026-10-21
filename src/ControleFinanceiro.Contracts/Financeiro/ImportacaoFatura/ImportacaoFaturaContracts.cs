namespace ControleFinanceiro.Contracts.Financeiro.ImportacaoFatura;

public sealed record ImportacaoFaturaItemPreview(
    DateOnly DataTransacao,
    string Descricao,
    decimal Valor,
    bool JaImportado,
    string ChaveImportacao,
    DateOnly? DataVencimentoFatura = null,
    int NumeroParcela = 1,
    int QuantidadeParcelas = 1);

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
    Guid? ContaGerencialId = null,
    DateOnly? DataVencimentoFatura = null,
    int NumeroParcela = 1,
    int QuantidadeParcelas = 1);  // sobrescreve ContaGerencialPadraoId quando informado

public sealed record ConfirmarImportacaoFaturaRequest(
    Guid CartaoId,
    Guid RecebedorPadraoId,
    IReadOnlyCollection<ImportacaoFaturaItemConfirmar> Itens,
    Guid? FormaPagamentoId = null,
    Guid? ContaGerencialPadraoId = null);

public sealed record ConfirmarImportacaoFaturaResponse(
    int ContasCriadas,
    int ContasDuplicadas);
