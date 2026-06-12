using ControleFinanceiro.Contracts.Filters;

namespace ControleFinanceiro.Contracts.Cadastros.Pessoas;

public enum PessoaTipo
{
    Fisica = 1,
    Juridica = 2
}

public enum PessoaChavePixTipo
{
    CpfCnpj = 1,
    Email = 2,
    Telefone = 3,
    Aleatoria = 4
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
    string? Observacao,
    IReadOnlyCollection<PessoaChavePixRequest>? ChavesPix);

public sealed record AtualizarPessoaRequest(
    string Nome,
    PessoaTipo TipoPessoa,
    string? CpfCnpj,
    string? Email,
    string? Telefone,
    string? Observacao,
    IReadOnlyCollection<PessoaChavePixRequest>? ChavesPix);

public sealed record PessoaChavePixRequest(
    PessoaChavePixTipo Tipo,
    string Chave);

public sealed record PessoaChavePixResponse(
    PessoaChavePixTipo Tipo,
    string Chave);

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
    IReadOnlyCollection<PessoaChavePixResponse> ChavesPix,
    bool Ativo,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
