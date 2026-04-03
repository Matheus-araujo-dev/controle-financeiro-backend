namespace ControleFinanceiro.Contracts.Common;

public sealed record PagedResult<T>(
    IReadOnlyCollection<T> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages)
{
    public static PagedResult<T> Create(IEnumerable<T> items, int page, int pageSize, int totalItems)
    {
        var normalizedPage = page < 1 ? 1 : page;
        var normalizedPageSize = pageSize < 1 ? 20 : pageSize;
        var normalizedTotalItems = totalItems < 0 ? 0 : totalItems;
        var normalizedItems = items.ToArray();
        var totalPages = normalizedTotalItems == 0
            ? 0
            : (int)Math.Ceiling(normalizedTotalItems / (double)normalizedPageSize);

        return new PagedResult<T>(
            normalizedItems,
            normalizedPage,
            normalizedPageSize,
            normalizedTotalItems,
            totalPages);
    }
}
