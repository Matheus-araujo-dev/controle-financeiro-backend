using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Contracts.Filters;
using ControleFinanceiro.Contracts.Financeiro.Common;

namespace ControleFinanceiro.Contracts.Financeiro.ContasPagar;

/// <summary>
/// Request para cursor pagination — use AfterCursor retornado pela resposta anterior.
/// </summary>
public sealed class ContaPagarCursorQueryRequest : CursorPageRequest
{
    public string? Search { get; init; }
    public Guid? RecebedorId { get; init; }
    public DateOnly? DataVencimentoInicial { get; init; }
    public DateOnly? DataVencimentoFinal { get; init; }
    public string[]? StatusCodigos { get; init; }
}

public enum LancamentoOrigem
{
    Manual = 1,
    Recorrencia = 2,
    Importacao = 3
}

public sealed record ContaPagarListQueryRequest : ListQueryRequest
{
    public string? NumeroDocumento { get; init; }

    public string? Descricao { get; init; }

    public Guid? RecebedorId { get; init; }

    public IReadOnlyCollection<Guid>? RecebedorIds { get; init; }

    public Guid? FormaPagamentoId { get; init; }

    public IReadOnlyCollection<Guid>? FormaPagamentoIds { get; init; }

    public string? StatusCodigo { get; init; }

    public IReadOnlyCollection<string>? StatusCodigos { get; init; }

    public DateOnly? DataEmissaoInicial { get; init; }

    public DateOnly? DataEmissaoFinal { get; init; }

    public DateOnly? DataVencimentoInicial { get; init; }

    public DateOnly? DataVencimentoFinal { get; init; }

    public decimal? ValorMinimo { get; init; }

    public decimal? ValorMaximo { get; init; }

    public bool? EhRecorrente { get; init; }
}

public sealed record CriarContaPagarRequest(
    Guid? OrigemCompraPlanejadaId,
    string? NumeroDocumento,
    DateOnly DataEmissao,
    Guid? ResponsavelCompraId,
    Guid RecebedorId,
    DateOnly DataVencimento,
    Guid FormaPagamentoId,
    Guid? CartaoId,
    Guid? ContaBancariaId,
    DateOnly? DataLiquidacao,
    decimal ValorOriginal,
    decimal ValorDesconto,
    decimal ValorJuros,
    decimal ValorMulta,
    int QuantidadeParcelas,
    string Descricao,
    string? Observacao,
    IReadOnlyCollection<RateioRequest> Rateios,
    RecorrenciaConfigRequest? Recorrencia);

public sealed record AtualizarContaPagarRequest(
    Guid Id,
    string? NumeroDocumento,
    DateOnly DataEmissao,
    Guid? ResponsavelCompraId,
    Guid RecebedorId,
    DateOnly DataVencimento,
    Guid FormaPagamentoId,
    Guid? CartaoId,
    Guid? ContaBancariaId,
    DateOnly? DataLiquidacao,
    decimal ValorOriginal,
    decimal ValorDesconto,
    decimal ValorJuros,
    decimal ValorMulta,
    int QuantidadeParcelas,
    string Descricao,
    string? Observacao,
    IReadOnlyCollection<RateioRequest> Rateios,
    RecorrenciaConfigRequest? Recorrencia);

public sealed record LiquidarContaPagarRequest(
    decimal ValorLiquidacao,
    DateOnly DataLiquidacao,
    Guid ContaBancariaId,
    Guid? FormaPagamentoId = null,
    bool AtualizarValorConta = false);

public sealed record CancelarContaPagarRequest(
    bool? CancelarPlanejamentoRelacionado = null);

public sealed record ContaPagarResumoResponse(
    Guid Id,
    string? NumeroDocumento,
    string Descricao,
    Guid RecebedorId,
    string RecebedorNome,
    string? ResponsavelNome,
    DateOnly DataEmissao,
    DateOnly DataVencimento,
    DateOnly? DataLiquidacao,
    Guid FormaPagamentoId,
    string FormaPagamentoNome,
    decimal ValorLiquido,
    string StatusCodigo,
    string StatusNome,
    int QuantidadeParcelas,
    int NumeroParcela,
    Guid? GrupoParcelamentoId,
    bool EhRecorrente);

public sealed record ContaPagarListSummaryResponse(
    int TotalRegistros,
    decimal ValorTotal,
    decimal TotalPendente,
    decimal TotalVencido,
    decimal TotalVencendoHoje,
    decimal TotalLiquidado);

public sealed record ContaPagarListResponse(
    IReadOnlyCollection<ContaPagarResumoResponse> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    ContaPagarListSummaryResponse Summary);

public sealed record ContaPagarDetalheResponse(
    Guid Id,
    string? NumeroDocumento,
    DateOnly DataEmissao,
    Guid? ResponsavelCompraId,
    string? ResponsavelCompraNome,
    Guid RecebedorId,
    string RecebedorNome,
    DateOnly DataVencimento,
    DateOnly? DataLiquidacao,
    Guid FormaPagamentoId,
    string FormaPagamentoNome,
    bool FormaPagamentoEhCartao,
    bool FormaPagamentoBaixarAutomaticamente,
    Guid? CartaoId,
    string? CartaoNome,
    Guid? ContaBancariaId,
    string? ContaBancariaNome,
    decimal ValorOriginal,
    decimal ValorDesconto,
    decimal ValorJuros,
    decimal ValorMulta,
    decimal ValorLiquido,
    int QuantidadeParcelas,
    int NumeroParcela,
    Guid? GrupoParcelamentoId,
    Guid? OrigemCompraPlanejadaId,
    string Descricao,
    string? Observacao,
    string StatusCodigo,
    string StatusNome,
    bool EhRecorrente,
    LancamentoOrigem Origem,
    RecorrenciaResponse? Recorrencia,
    string? CompetenciaFaturaCartao,
    DateOnly? DataFechamentoFaturaCartao,
    DateOnly? DataVencimentoFaturaCartao,
    IReadOnlyCollection<RateioResponse> Rateios,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
