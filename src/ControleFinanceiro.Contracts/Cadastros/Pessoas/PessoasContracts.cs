using ControleFinanceiro.Contracts.Filters;

namespace ControleFinanceiro.Contracts.Cadastros.Pessoas;

public enum PessoaTipo
{
    Fisica = 1,
    Juridica = 2
}

public sealed record PessoaListQueryRequest : ListQueryRequest
{
    public PessoaTipo? TipoPessoa { get; init; }

    public bool? Ativo { get; init; }
}

public sealed record CriarPessoaRequest(
    string Nome,
    PessoaTipo TipoPessoa,
    string? CpfCnpj,
    string? Email,
    string? Telefone,
    string? Observacao);

public sealed record AtualizarPessoaRequest(
    string Nome,
    PessoaTipo TipoPessoa,
    string? CpfCnpj,
    string? Email,
    string? Telefone,
    string? Observacao);

public sealed record PessoaResumoResponse(
    Guid Id,
    string Nome,
    PessoaTipo TipoPessoa,
    string? CpfCnpj,
    string? Email,
    string? Telefone,
    bool Ativo);

public sealed record PessoaDetalheResponse(
    Guid Id,
    string Nome,
    PessoaTipo TipoPessoa,
    string? CpfCnpj,
    string? Email,
    string? Telefone,
    string? Observacao,
    bool Ativo,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
