using ControleFinanceiro.Contracts.Filters;

namespace ControleFinanceiro.Contracts.Cadastros.ContasGerenciais;

public enum ContaGerencialTipo
{
    Receita = 1,
    Despesa = 2
}

public sealed record ContaGerencialListQueryRequest : ListQueryRequest
{
    public ContaGerencialTipo? Tipo { get; init; }

    public Guid? ContaPaiId { get; init; }

    public bool? Ativo { get; init; }
}

public sealed record CriarContaGerencialRequest(
    string? Codigo,
    string Descricao,
    ContaGerencialTipo Tipo,
    Guid? ContaPaiId,
    bool Ativo);

public sealed record AtualizarContaGerencialRequest(
    string? Codigo,
    string Descricao,
    ContaGerencialTipo Tipo,
    Guid? ContaPaiId,
    bool Ativo);

public sealed record ContaGerencialResumoResponse(
    Guid Id,
    string? Codigo,
    string Descricao,
    ContaGerencialTipo Tipo,
    Guid? ContaPaiId,
    string? ContaPaiDescricao,
    bool Ativo);

public sealed record ContaGerencialDetalheResponse(
    Guid Id,
    string? Codigo,
    string Descricao,
    ContaGerencialTipo Tipo,
    Guid? ContaPaiId,
    string? ContaPaiDescricao,
    bool Ativo,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
