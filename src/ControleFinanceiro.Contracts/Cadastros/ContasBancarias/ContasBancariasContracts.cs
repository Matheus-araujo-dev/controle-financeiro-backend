using ControleFinanceiro.Contracts.Filters;

namespace ControleFinanceiro.Contracts.Cadastros.ContasBancarias;

public sealed record ContaBancariaListQueryRequest : ListQueryRequest
{
    public string? Banco { get; init; }

    public string? Agencia { get; init; }

    public string? NumeroConta { get; init; }

    public string? TipoConta { get; init; }

    public bool? Ativo { get; init; }

    /// <summary>Filtro multi-select por tipo de conta (Corrente, Poupanca, Investimento...).</summary>
    public IReadOnlyList<string>? TiposConta { get; init; }
}

public sealed record ContaBancariaListSummaryResponse(
    int Total,
    int Ativas,
    decimal SaldoTotal,
    decimal CreditoDisponivel);

public sealed record ContaBancariaListResponse(
    IReadOnlyCollection<ContaBancariaResumoResponse> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    ContaBancariaListSummaryResponse Summary);

public sealed record CriarContaBancariaRequest(
    string Nome,
    string Banco,
    string? Agencia,
    string? NumeroConta,
    string? TipoConta,
    decimal SaldoInicial,
    DateOnly DataSaldoInicial,
    decimal? LimiteCartoesCompartilhado,
    bool Ativo);

public sealed record AtualizarContaBancariaRequest(
    string Nome,
    string Banco,
    string? Agencia,
    string? NumeroConta,
    string? TipoConta,
    decimal SaldoInicial,
    DateOnly DataSaldoInicial,
    decimal? LimiteCartoesCompartilhado,
    bool Ativo);

public sealed record ContaBancariaResumoResponse(
    Guid Id,
    string Nome,
    string Banco,
    string? Agencia,
    string? NumeroConta,
    string? TipoConta,
    decimal SaldoInicial,
    DateOnly DataSaldoInicial,
    decimal SaldoAtual,
    decimal? LimiteCartoesCompartilhado,
    decimal LimiteCartoesComprometido,
    decimal? LimiteCartoesDisponivel,
    bool Ativo);

public sealed record ContaBancariaDetalheResponse(
    Guid Id,
    string Nome,
    string Banco,
    string? Agencia,
    string? NumeroConta,
    string? TipoConta,
    decimal SaldoInicial,
    DateOnly DataSaldoInicial,
    decimal SaldoAtual,
    decimal? LimiteCartoesCompartilhado,
    decimal LimiteCartoesComprometido,
    decimal? LimiteCartoesDisponivel,
    bool Ativo,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
