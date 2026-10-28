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

    public IReadOnlyList<ContaGerencialTipo>? Tipos { get; init; }

    public Guid? ContaPaiId { get; init; }

    public string? ContaPai { get; init; }

    public Guid? MembroPadraoId { get; init; }

    public string? MembroPadrao { get; init; }

    public bool? Ativo { get; init; }

    public bool? AceitaLancamentos { get; init; }

    public bool? EhPadraoRecebimentoFaturaCartao { get; init; }
}

public sealed record CriarContaGerencialRequest(
    string? Codigo,
    string Descricao,
    ContaGerencialTipo Tipo,
    Guid? ContaPaiId,
    Guid? MembroPadraoId,
    bool Ativo,
    bool EhPadraoRecebimentoFaturaCartao,
    Guid? ContaGerencialContrariaId = null);

public sealed record AtualizarContaGerencialRequest(
    string? Codigo,
    string Descricao,
    ContaGerencialTipo Tipo,
    Guid? ContaPaiId,
    Guid? MembroPadraoId,
    bool Ativo,
    bool EhPadraoRecebimentoFaturaCartao,
    Guid? ContaGerencialContrariaId = null);

public sealed record ContaGerencialResumoResponse(
    Guid Id,
    string? Codigo,
    string Descricao,
    ContaGerencialTipo Tipo,
    Guid? ContaPaiId,
    string? ContaPaiDescricao,
    Guid? MembroPadraoId,
    string? MembroPadraoNome,
    bool Ativo,
    bool AceitaLancamentos,
    bool EhPadraoRecebimentoFaturaCartao,
    Guid? ContaGerencialContrariaId = null,
    string? ContaGerencialContrariaNome = null);

public sealed record ContaGerencialDetalheResponse(
    Guid Id,
    string? Codigo,
    string Descricao,
    ContaGerencialTipo Tipo,
    Guid? ContaPaiId,
    string? ContaPaiDescricao,
    Guid? MembroPadraoId,
    string? MembroPadraoNome,
    bool Ativo,
    bool AceitaLancamentos,
    bool EhPadraoRecebimentoFaturaCartao,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    Guid? ContaGerencialContrariaId = null,
    string? ContaGerencialContrariaNome = null);

public sealed record SeedPlanoInicialResponse(int ContasCriadas);
