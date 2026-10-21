using ControleFinanceiro.Contracts.Filters;

namespace ControleFinanceiro.Contracts.Cadastros.Cartoes;

public sealed record CartaoListQueryRequest : ListQueryRequest
{
    public string? Bandeira { get; init; }

    public string? NumeroFinal { get; init; }

    public int? DiaFechamentoFatura { get; init; }

    public int? DiaVencimentoFatura { get; init; }

    public Guid? ContaBancariaPagamentoPadraoId { get; init; }

    public bool? Ativo { get; init; }
}

public sealed record CriarCartaoRequest(
    string Nome,
    string Bandeira,
    string NumeroFinal,
    int DiaFechamentoFatura,
    int DiaVencimentoFatura,
    Guid? ContaBancariaPagamentoPadraoId,
    decimal? LimiteCredito,
    bool Ativo,
    string? Icone = null,
    string? Cor = null,
    Guid? RecebedorPadraoFaturaId = null,
    Guid? FormaPagamentoPadraoFaturaId = null);

public sealed record AtualizarCartaoRequest(
    string Nome,
    string Bandeira,
    string NumeroFinal,
    int DiaFechamentoFatura,
    int DiaVencimentoFatura,
    Guid? ContaBancariaPagamentoPadraoId,
    decimal? LimiteCredito,
    bool Ativo,
    string? Icone = null,
    string? Cor = null,
    Guid? RecebedorPadraoFaturaId = null,
    Guid? FormaPagamentoPadraoFaturaId = null);

public sealed record CartaoResumoResponse(
    Guid Id,
    string Nome,
    string Bandeira,
    string NumeroFinal,
    int DiaFechamentoFatura,
    int DiaVencimentoFatura,
    Guid? ContaBancariaPagamentoPadraoId,
    decimal? LimiteCredito,
    bool UsaLimiteCompartilhado,
    decimal? LimiteEfetivo,
    decimal LimiteComprometido,
    decimal? LimiteDisponivel,
    bool Ativo,
    string? Icone,
    string? Cor,
    Guid? RecebedorPadraoFaturaId = null,
    Guid? FormaPagamentoPadraoFaturaId = null);

public sealed record CartaoDetalheResponse(
    Guid Id,
    string Nome,
    string Bandeira,
    string NumeroFinal,
    int DiaFechamentoFatura,
    int DiaVencimentoFatura,
    Guid? ContaBancariaPagamentoPadraoId,
    decimal? LimiteCredito,
    bool UsaLimiteCompartilhado,
    decimal? LimiteEfetivo,
    decimal LimiteComprometido,
    decimal? LimiteDisponivel,
    bool Ativo,
    string? Icone,
    string? Cor,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    Guid? RecebedorPadraoFaturaId = null,
    Guid? FormaPagamentoPadraoFaturaId = null);
