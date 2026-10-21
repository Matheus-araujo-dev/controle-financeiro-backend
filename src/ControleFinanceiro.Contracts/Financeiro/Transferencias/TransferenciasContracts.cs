namespace ControleFinanceiro.Contracts.Financeiro.Transferencias;

public record CriarTransferenciaRequest(
    Guid ContaBancariaOrigemId,
    Guid ContaBancariaDestinoId,
    decimal Valor,
    DateOnly DataTransferencia,
    string? Descricao);

public record TransferenciaResumoResponse(
    Guid Id,
    Guid ContaBancariaOrigemId,
    string OrigemNome,
    Guid ContaBancariaDestinoId,
    string DestinoNome,
    decimal Valor,
    DateOnly DataTransferencia,
    string? Descricao,
    bool Cancelada,
    DateTime CreatedAtUtc);

public record TransferenciaListResponse(
    IReadOnlyList<TransferenciaResumoResponse> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);

public record TransferenciaListQuery(
    int Page = 1,
    int PageSize = 20,
    Guid? ContaBancariaOrigemId = null,
    Guid? ContaBancariaDestinoId = null,
    DateOnly? DataInicial = null,
    DateOnly? DataFinal = null,
    bool? Cancelada = null);
