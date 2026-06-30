namespace ControleFinanceiro.Contracts.Common;

/// <summary>
/// Request base para cursor pagination — sem SKIP, infinitamente escalável.
/// O cursor é opaco para o cliente (base64 do campo de ordenação + Id).
/// </summary>
public class CursorPageRequest
{
    public string? AfterCursor { get; init; }
    public int PageSize { get; init; } = 20;

    public int NormalizedPageSize => PageSize is < 1 or > 200 ? 20 : PageSize;
}
