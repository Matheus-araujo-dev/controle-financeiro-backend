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

    public bool? AceitaLancamentos { get; init; }
}

public sealed record CriarContaGerencialRequest(
    string? Codigo,
    string Descricao,
    ContaGerencialTipo Tipo,
    Guid? ContaPaiId,
    Guid? ResponsavelPadraoId,
    bool Ativo,
    bool EhPadraoRecebimentoFaturaCartao);

public sealed record AtualizarContaGerencialRequest(
    string? Codigo,
    string Descricao,
    ContaGerencialTipo Tipo,
    Guid? ContaPaiId,
    Guid? ResponsavelPadraoId,
    bool Ativo,
    bool EhPadraoRecebimentoFaturaCartao);

public sealed record ContaGerencialResumoResponse(
    Guid Id,
    string? Codigo,
    string Descricao,
    ContaGerencialTipo Tipo,
    Guid? ContaPaiId,
    string? ContaPaiDescricao,
    Guid? ResponsavelPadraoId,
    string? ResponsavelPadraoNome,
    bool Ativo,
    bool AceitaLancamentos,
    bool EhPadraoRecebimentoFaturaCartao);

public sealed record ContaGerencialDetalheResponse(
    Guid Id,
    string? Codigo,
    string Descricao,
    ContaGerencialTipo Tipo,
    Guid? ContaPaiId,
    string? ContaPaiDescricao,
    Guid? ResponsavelPadraoId,
    string? ResponsavelPadraoNome,
    bool Ativo,
    bool AceitaLancamentos,
    bool EhPadraoRecebimentoFaturaCartao,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
