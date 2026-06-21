using ControleFinanceiro.Contracts.Filters;

namespace ControleFinanceiro.Contracts.PlanejamentoCompras;

public sealed record CompraPlanejadaListQueryRequest : ListQueryRequest
{
    public string? Prioridade { get; init; }

    public IReadOnlyList<string>? Prioridades { get; init; }

    public string? Status { get; init; }

    public IReadOnlyList<string>? Statuses { get; init; }

    public Guid? ResponsavelId { get; init; }

    public Guid? ContaGerencialId { get; init; }

    public bool? Parcelavel { get; init; }

    public DateOnly? DataDesejadaInicial { get; init; }

    public DateOnly? DataDesejadaFinal { get; init; }

    public decimal? ValorEstimadoMin { get; init; }

    public decimal? ValorEstimadoMax { get; init; }

    public string? Link { get; init; }
}

public sealed record CriarCompraPlanejadaRequest(
    string Titulo,
    string? Descricao,
    decimal ValorEstimado,
    DateOnly? DataDesejada,
    string Prioridade,
    string Status,
    bool Parcelavel,
    int? QuantidadeParcelasDesejada,
    Guid ContaGerencialId,
    Guid ResponsavelId,
    string? Link,
    string? Observacao);

public sealed record AtualizarCompraPlanejadaRequest(
    string Titulo,
    string? Descricao,
    decimal ValorEstimado,
    DateOnly? DataDesejada,
    string Prioridade,
    string Status,
    bool Parcelavel,
    int? QuantidadeParcelasDesejada,
    Guid ContaGerencialId,
    Guid ResponsavelId,
    string? Link,
    string? Observacao);

public sealed record RealizarCompraPlanejadaRequest(
    DateOnly DataCompra,
    DateOnly? DataVencimento,
    Guid RecebedorId,
    Guid FormaPagamentoId,
    Guid? CartaoId,
    Guid? ContaBancariaId,
    int QuantidadeParcelas,
    string? NumeroDocumento,
    string? Descricao,
    string? Observacao);

public sealed record CompraPlanejadaResumoResponse(
    Guid Id,
    string Titulo,
    decimal ValorEstimado,
    DateOnly? DataDesejada,
    string Prioridade,
    string Status,
    bool Parcelavel,
    int? QuantidadeParcelasDesejada,
    Guid ContaGerencialId,
    string ContaGerencialDescricao,
    Guid ResponsavelId,
    string ResponsavelNome,
    string? Link,
    Guid? ContaPagarGeradaId,
    DateTime? ConvertidaEmContaPagarEmUtc);

public sealed record CompraPlanejadaListSummaryResponse(
    int TotalRegistros,
    decimal ValorTotalEstimado);

public sealed record CompraPlanejadaListResponse(
    IReadOnlyCollection<CompraPlanejadaResumoResponse> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    CompraPlanejadaListSummaryResponse Summary);

public sealed record CompraPlanejadaDetalheResponse(
    Guid Id,
    string Titulo,
    string? Descricao,
    decimal ValorEstimado,
    DateOnly? DataDesejada,
    string Prioridade,
    string Status,
    bool Parcelavel,
    int? QuantidadeParcelasDesejada,
    Guid ContaGerencialId,
    string ContaGerencialDescricao,
    Guid ResponsavelId,
    string ResponsavelNome,
    string? Link,
    string? Observacao,
    Guid? ContaPagarGeradaId,
    DateTime? ConvertidaEmContaPagarEmUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
