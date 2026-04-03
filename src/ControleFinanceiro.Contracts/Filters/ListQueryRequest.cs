namespace ControleFinanceiro.Contracts.Filters;

public sealed record ListQueryRequest : Common.PageRequest
{
    public string? Search { get; init; }
}
