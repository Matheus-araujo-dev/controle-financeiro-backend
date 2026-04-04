using ControleFinanceiro.Contracts.Filters;

namespace ControleFinanceiro.Contracts.Cadastros.ContasBancarias;

public sealed record ContaBancariaListQueryRequest : ListQueryRequest
{
    public string? Banco { get; init; }

    public bool? Ativo { get; init; }
}

public sealed record CriarContaBancariaRequest(
    string Nome,
    string Banco,
    string? Agencia,
    string? NumeroConta,
    string? TipoConta,
    decimal SaldoInicial,
    DateOnly DataSaldoInicial,
    bool Ativo);

public sealed record AtualizarContaBancariaRequest(
    string Nome,
    string Banco,
    string? Agencia,
    string? NumeroConta,
    string? TipoConta,
    decimal SaldoInicial,
    DateOnly DataSaldoInicial,
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
    bool Ativo,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
