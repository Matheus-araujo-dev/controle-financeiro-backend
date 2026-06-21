using ControleFinanceiro.Contracts.Filters;

namespace ControleFinanceiro.Contracts.Cadastros.FormasPagamento;

public enum FormaPagamentoTipo
{
    Dinheiro = 1,
    Pix = 2,
    Boleto = 3,
    Transferencia = 4,
    Debito = 5,
    Credito = 6,
    Outro = 7
}

public sealed record FormaPagamentoListQueryRequest : ListQueryRequest
{
    public FormaPagamentoTipo? Tipo { get; init; }

    public IReadOnlyList<FormaPagamentoTipo>? Tipos { get; init; }

    public bool? EhCartao { get; init; }

    public bool? BaixarAutomaticamente { get; init; }

    public bool? Ativo { get; init; }
}

public sealed record CriarFormaPagamentoRequest(
    string Nome,
    FormaPagamentoTipo Tipo,
    bool EhCartao,
    bool BaixarAutomaticamente,
    bool Ativo);

public sealed record AtualizarFormaPagamentoRequest(
    string Nome,
    FormaPagamentoTipo Tipo,
    bool EhCartao,
    bool BaixarAutomaticamente,
    bool Ativo);

public sealed record FormaPagamentoResumoResponse(
    Guid Id,
    string Nome,
    FormaPagamentoTipo Tipo,
    bool EhCartao,
    bool BaixarAutomaticamente,
    bool Ativo);

public sealed record FormaPagamentoDetalheResponse(
    Guid Id,
    string Nome,
    FormaPagamentoTipo Tipo,
    bool EhCartao,
    bool BaixarAutomaticamente,
    bool Ativo,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
