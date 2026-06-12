using ControleFinanceiro.Domain.Events;

namespace ControleFinanceiro.Domain.Financeiro.Events;

public sealed class ContaReceberRecebidaEvent : DomainEventBase
{
    public Guid ContaReceberId { get; }

    public string NumeroDocumento { get; }

    public Guid PagadorId { get; }

    public string Descricao { get; }

    public decimal ValorLiquido { get; }

    public DateOnly DataLiquidacao { get; }

    public Guid ContaBancariaId { get; }

    public ContaReceberRecebidaEvent(
        Guid contaReceberId,
        string? numeroDocumento,
        Guid pagadorId,
        string descricao,
        decimal valorLiquido,
        DateOnly dataLiquidacao,
        Guid contaBancariaId)
    {
        ContaReceberId = contaReceberId;
        NumeroDocumento = numeroDocumento ?? string.Empty;
        PagadorId = pagadorId;
        Descricao = descricao;
        ValorLiquido = valorLiquido;
        DataLiquidacao = dataLiquidacao;
        ContaBancariaId = contaBancariaId;
    }
}