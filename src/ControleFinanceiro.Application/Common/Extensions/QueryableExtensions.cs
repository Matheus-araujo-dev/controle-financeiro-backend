using System.Linq.Expressions;

namespace ControleFinanceiro.Application.Common.Extensions;

public static class QueryableExtensions
{
    private const int LargeListThreshold = 50;

    public static IQueryable<T> WhereIn<T, TKey>(
        this IQueryable<T> query,
        Expression<Func<T, TKey>> keySelector,
        IEnumerable<TKey> values)
    {
        var valueList = values.ToList();

        if (valueList.Count == 0)
        {
            return query;
        }

        if (valueList.Count <= LargeListThreshold)
        {
            var constant = Expression.Constant(valueList, typeof(List<TKey>));
            var contains = Expression.Call(
                typeof(Enumerable),
                "Contains",
                [typeof(TKey)],
                constant,
                keySelector.Body);
            var lambda = Expression.Lambda<Func<T, bool>>(contains, keySelector.Parameters);
            return query.Where(lambda);
        }

        var idsQuery = valueList.AsQueryable();
        return query.Join(
            idsQuery,
            keySelector,
            id => id,
            (entity, _) => entity);
    }

    public static IQueryable<T> WhereIn<T, TKey>(
        this IQueryable<T> query,
        Expression<Func<T, TKey?>> keySelector,
        IEnumerable<TKey> values) where TKey : struct
    {
        var valueList = values.ToList();

        if (valueList.Count == 0)
        {
            return query;
        }

        var constant = Expression.Constant(valueList, typeof(List<TKey>));
        var bodyAsNonNullable = Expression.Convert(keySelector.Body, typeof(TKey));
        var contains = Expression.Call(
            typeof(Enumerable),
            "Contains",
            [typeof(TKey)],
            constant,
            bodyAsNonNullable);
        var nullCheck = Expression.NotEqual(keySelector.Body, Expression.Constant(null, typeof(TKey?)));
        var combined = Expression.AndAlso(nullCheck, contains);
        var lambda = Expression.Lambda<Func<T, bool>>(combined, keySelector.Parameters);
        return query.Where(lambda);
    }
}