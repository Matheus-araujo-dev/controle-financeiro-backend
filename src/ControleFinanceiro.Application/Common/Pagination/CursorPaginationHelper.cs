using System.Text;
using ControleFinanceiro.Contracts.Common;

namespace ControleFinanceiro.Application.Common.Pagination;

/// <summary>
/// Cursor codificado como base64 de "{sortValue}|{id}".
/// O campo de sort e o Id formam juntos um cursor estável e único.
/// </summary>
public static class CursorPaginationHelper
{
    public static string EncodeCursor(string sortValue, Guid id) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{sortValue}|{id}"));

    public static (string SortValue, Guid Id)? DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;
        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var sep = raw.LastIndexOf('|');
            if (sep < 0) return null;
            var sortValue = raw[..sep];
            var id = Guid.Parse(raw[(sep + 1)..]);
            return (sortValue, id);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Aplica cursor pagination a uma lista já materializada e mapeada.
    /// </summary>
    public static CursorPagedResult<T> FromList<T>(
        List<T> items,
        int pageSize,
        Func<T, string> getSortValue,
        Func<T, Guid> getId)
    {
        var hasMore = items.Count > pageSize;
        if (hasMore) items.RemoveAt(items.Count - 1);

        string? nextCursor = null;
        if (hasMore && items.Count > 0)
        {
            var last = items[^1];
            nextCursor = EncodeCursor(getSortValue(last), getId(last));
        }

        return new CursorPagedResult<T>(items, nextCursor, hasMore);
    }
}
