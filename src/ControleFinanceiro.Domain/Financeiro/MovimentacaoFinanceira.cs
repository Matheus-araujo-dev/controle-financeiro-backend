using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.Financeiro;

public sealed class MovimentacaoFinanceira : AuditableEntity
{
    private MovimentacaoFinanceira()
    {
    }

    public DateOnly DataMovimentacao { get; private set; }

    public TipoMovimentacao Tipo { get; private set; }

    public NaturezaMovimentacao Natureza { get; private set; }

    public Guid? ContaBancariaId { get; private set; }

    public Guid? ContaPagarId { get; private set; }

    public Guid? ContaReceberId { get; private set; }

    public Guid? FaturaCartaoId { get; private set; }

    public decimal Valor { get; private set; }

    public Guid StatusMovimentacaoId { get; private set; }

    public string? Observacao { get; private set; }

    public DateOnly? DataConciliacao { get; private set; }

    public static MovimentacaoFinanceira CriarLiquidacaoContaPagar(
        Guid contaPagarId,
        Guid contaBancariaId,
        DateOnly dataMovimentacao,
        decimal valor,
        Guid statusMovimentacaoId,
        string? observacao)
    {
        return Criar(
            dataMovimentacao,
            TipoMovimentacao.Saida,
            NaturezaMovimentacao.Realizada,
            contaBancariaId,
            contaPagarId,
            null,
            valor,
            statusMovimentacaoId,
            observacao);
    }

    public static MovimentacaoFinanceira CriarLiquidacaoContaReceber(
        Guid contaReceberId,
        Guid contaBancariaId,
        DateOnly dataMovimentacao,
        decimal valor,
        Guid statusMovimentacaoId,
        string? observacao)
    {
        return Criar(
            dataMovimentacao,
            TipoMovimentacao.Entrada,
            NaturezaMovimentacao.Realizada,
            contaBancariaId,
            null,
            contaReceberId,
            valor,
            statusMovimentacaoId,
            observacao);
    }

    private static MovimentacaoFinanceira Criar(
        DateOnly dataMovimentacao,
        TipoMovimentacao tipo,
        NaturezaMovimentacao natureza,
        Guid? contaBancariaId,
        Guid? contaPagarId,
        Guid? contaReceberId,
        decimal valor,
        Guid statusMovimentacaoId,
        string? observacao)
    {
        if (valor <= 0)
        {
            throw new ArgumentException("Valor da movimentacao deve ser maior que zero.", nameof(valor));
        }

        return new MovimentacaoFinanceira
        {
            DataMovimentacao = dataMovimentacao,
            Tipo = tipo,
            Natureza = natureza,
            ContaBancariaId = contaBancariaId,
            ContaPagarId = contaPagarId,
            ContaReceberId = contaReceberId,
            Valor = decimal.Round(valor, 2, MidpointRounding.AwayFromZero),
            StatusMovimentacaoId = statusMovimentacaoId,
            Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim()
        };
    }
}
