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
    /// <summary>Filtro de tipo único (mantido por compatibilidade). Prefira <see cref="TiposPessoa"/>.</summary>
    public PessoaTipo? TipoPessoa { get; init; }

    /// <summary>Filtro multi-select por tipo de pessoa.</summary>
    public IReadOnlyList<PessoaTipo>? TiposPessoa { get; init; }

    public bool? Ativo { get; init; }

    /// <summary>Filtro por documento (CPF/CNPJ), busca parcial.</summary>
    public string? Documento { get; init; }

    /// <summary>Filtro por e-mail, busca parcial.</summary>
    public string? Email { get; init; }

    /// <summary>Filtro por telefone, busca parcial.</summary>
    public string? Telefone { get; init; }
}

public sealed record PessoaListSummaryResponse(
    int Total,
    int Ativos,
    int Inativos,
    int Fisicas,
    int Juridicas);

public sealed record PessoaListResponse(
    IReadOnlyCollection<PessoaResumoResponse> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    PessoaListSummaryResponse Summary);

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
