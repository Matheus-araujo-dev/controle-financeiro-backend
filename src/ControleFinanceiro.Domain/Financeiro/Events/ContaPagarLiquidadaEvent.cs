using ControleFinanceiro.Domain.Events;

namespace ControleFinanceiro.Domain.Financeiro.Events;

public sealed class ContaPagarLiquidadaEvent : DomainEventBase
{
    public Guid ContaPagarId { get; }

    public decimal ValorLiquido { get; }

    public DateOnly DataLiquidacao { get; }

    public Guid ContaBancariaId { get; }

    public string Descricao { get; }

    public ContaPagarLiquidadaEvent(
        Guid contaPagarId,
        decimal valorLiquido,
        DateOnly dataLiquidacao,
        Guid contaBancariaId,
        string descricao)
    {
        ContaPagarId = contaPagarId;
        ValorLiquido = valorLiquido;
        DataLiquidacao = dataLiquidacao;
        ContaBancariaId = contaBancariaId;
        Descricao = descricao;
    }
}