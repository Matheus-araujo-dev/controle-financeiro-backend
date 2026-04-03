namespace ControleFinanceiro.Contracts.Common;

public record PageRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SortBy { get; init; }
    public SortDirection SortDirection { get; init; } = SortDirection.Asc;

    public int NormalizedPage => Page < 1 ? 1 : Page;
    public int NormalizedPageSize => PageSize < 1 ? 20 : PageSize;
}
