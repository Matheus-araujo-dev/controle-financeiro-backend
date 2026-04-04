namespace ControleFinanceiro.Contracts.Filters;

public record ListQueryRequest : Common.PageRequest
{
    public string? Search { get; init; }
}
