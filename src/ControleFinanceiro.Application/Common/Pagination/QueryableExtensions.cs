using ControleFinanceiro.Contracts.Common;
using System.Linq.Expressions;
using System.Reflection;

namespace ControleFinanceiro.Application.Common.Pagination;

public static class QueryableExtensions
{
    public static IQueryable<T> ApplyPagination<T>(this IQueryable<T> query, PageRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.SortBy))
        {
            var propertyInfo = typeof(T).GetProperty(request.SortBy, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            if (propertyInfo != null)
            {
                var parameter = Expression.Parameter(typeof(T), "x");
                var property = Expression.Property(parameter, propertyInfo);
                var method = request.SortDirection == SortDirection.Desc ? "OrderByDescending" : "OrderBy";
                var types = new[] { typeof(T), propertyInfo.PropertyType };
                var lambda = Expression.Lambda(property, parameter);

                var resultExpression = Expression.Call(
                    typeof(Queryable), method, types,
                    query.Expression, Expression.Quote(lambda));

                query = query.Provider.CreateQuery<T>(resultExpression);
            }
        }

        var skip = (request.NormalizedPage - 1) * request.NormalizedPageSize;
        return query.Skip(skip).Take(request.NormalizedPageSize);
    }
}
