using ControleFinanceiro.Domain.Financeiro;

namespace ControleFinanceiro.Contracts.Financeiro.Investimentos;

public record CriarInvestimentoRequest(
    string Nome,
    string? Emissor,
    TipoInvestimento Tipo,
    LiquidezInvestimento Liquidez,
    decimal ValorInvestido,
    DateOnly DataAplicacao,
    DateOnly? DataVencimento,
    decimal? TaxaAnual,
    Guid ContaBancariaVinculadaId);

public record AtualizarInvestimentoRequest(
    string Nome,
    string? Emissor,
    TipoInvestimento Tipo,
    LiquidezInvestimento Liquidez,
    DateOnly? DataVencimento,
    decimal? TaxaAnual);

public record AtualizarValorAtualRequest(decimal ValorAtual);

public record EncerrarInvestimentoRequest(decimal ValorResgate);

public record InvestimentoResumoResponse(
    Guid Id,
    string Nome,
    string? Emissor,
    TipoInvestimento Tipo,
    string TipoLabel,
    LiquidezInvestimento Liquidez,
    string LiquidezLabel,
    decimal ValorInvestido,
    decimal ValorAtual,
    decimal Rendimento,
    decimal RendimentoPercent,
    DateOnly DataAplicacao,
    DateOnly? DataVencimento,
    decimal? TaxaAnual,
    Guid ContaBancariaVinculadaId,
    string ContaBancariaNome,
    bool Encerrado,
    DateTime CreatedAtUtc);

public record InvestimentoListQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    TipoInvestimento? Tipo = null,
    bool? Encerrado = null,
    Guid? ContaBancariaVinculadaId = null);

public record InvestimentoListResponse(
    IReadOnlyList<InvestimentoResumoResponse> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);

public record IndicadoresBcbResponse(
    decimal? SelicAnual,
    decimal? CdiAnual,
    decimal? IpcaAcumulado12m,
    DateTime? AtualizadoEm);
