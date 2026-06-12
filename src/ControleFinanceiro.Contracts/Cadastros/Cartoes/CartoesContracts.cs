using ControleFinanceiro.Contracts.Filters;

namespace ControleFinanceiro.Contracts.Cadastros.Cartoes;

public sealed record CartaoListQueryRequest : ListQueryRequest
{
    public string? Bandeira { get; init; }

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
    bool Ativo);

public sealed record AtualizarCartaoRequest(
    string Nome,
    string Bandeira,
    string NumeroFinal,
    int DiaFechamentoFatura,
    int DiaVencimentoFatura,
    Guid? ContaBancariaPagamentoPadraoId,
    decimal? LimiteCredito,
    bool Ativo);

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
    bool Ativo);

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
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
