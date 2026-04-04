using ControleFinanceiro.Contracts.Common;

namespace ControleFinanceiro.Application.Common.Pagination;

public static class QueryableExtensions
{
    public static IQueryable<T> ApplyPagination<T>(this IQueryable<T> query, PageRequest request)
    {
        var skip = (request.NormalizedPage - 1) * request.NormalizedPageSize;
        return query.Skip(skip).Take(request.NormalizedPageSize);
    }
}
