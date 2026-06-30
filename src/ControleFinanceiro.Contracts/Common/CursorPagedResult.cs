namespace ControleFinanceiro.Contracts.Common;

/// <summary>
/// Resultado de cursor pagination: Items + NextCursor para a próxima página.
/// NextCursor null indica que não há mais páginas.
/// </summary>
public sealed record CursorPagedResult<T>(
    IReadOnlyCollection<T> Items,
    string? NextCursor,
    bool HasMore);
