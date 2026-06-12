using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.Financeiro;

public sealed class FaturaCartao : TenantEntity
{
    private FaturaCartao()
    {
    }

    public Guid CartaoId { get; private set; }

    public string Competencia { get; private set; } = string.Empty;

    public DateOnly DataFechamento { get; private set; }

    public DateOnly DataVencimento { get; private set; }

    public decimal ValorTotal { get; private set; }

    public DateOnly? DataPagamento { get; private set; }

    public Guid? ContaBancariaPagamentoId { get; private set; }

    public StatusFaturaCartao Status { get; private set; }

    public string? Observacao { get; private set; }

    public static FaturaCartao Criar(
        Guid cartaoId,
        string competencia,
        DateOnly dataFechamento,
        DateOnly dataVencimento,
        decimal valorTotal,
        string? observacao)
    {
        var fatura = new FaturaCartao();
        fatura.DefinirCampos(cartaoId, competencia, dataFechamento, dataVencimento, valorTotal, observacao);
        fatura.Status = StatusFaturaCartao.Aberta;
        return fatura;
    }

    public void AtualizarDadosGerados(
        DateOnly dataFechamento,
        DateOnly dataVencimento,
        decimal valorTotal)
    {
        if (Status == StatusFaturaCartao.Paga)
        {
            return;
        }

        DataFechamento = dataFechamento;
        DataVencimento = dataVencimento;
        AtualizarValorTotal(valorTotal);
    }

    /// <summary>
    /// Atualiza apenas o valor (estornos/ajustes) sem re-datar a fatura. Usado para
    /// faturas já fechadas: alterações nos dias do cartão não mexem em competências passadas.
    /// </summary>
    public void AtualizarValorTotal(decimal valorTotal)
    {
        if (Status == StatusFaturaCartao.Paga)
        {
            return;
        }

        if (valorTotal <= 0)
        {
            throw new ArgumentException("Valor total da fatura deve ser maior que zero.", nameof(valorTotal));
        }

        ValorTotal = decimal.Round(valorTotal, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Fatura cujo dia de fechamento já passou: imutável quanto a datas.</summary>
    public bool EstaFechada(DateOnly hoje) => DataFechamento < hoje;

    public void Pagar(DateOnly dataPagamento, Guid contaBancariaPagamentoId, string? observacao)
    {
        if (Status == StatusFaturaCartao.Paga)
        {
            throw new InvalidOperationException("Fatura ja foi paga.");
        }

        if (contaBancariaPagamentoId == Guid.Empty)
        {
            throw new ArgumentException("Conta bancaria de pagamento e obrigatoria.", nameof(contaBancariaPagamentoId));
        }

        DataPagamento = dataPagamento;
        ContaBancariaPagamentoId = contaBancariaPagamentoId;
        Status = StatusFaturaCartao.Paga;
        Observacao = string.IsNullOrWhiteSpace(observacao) ? Observacao : observacao.Trim();
    }

    public void ReabrirPagamento()
    {
        if (Status != StatusFaturaCartao.Paga)
        {
            throw new InvalidOperationException("Apenas faturas pagas podem ser reabertas.");
        }

        DataPagamento = null;
        ContaBancariaPagamentoId = null;
        Status = StatusFaturaCartao.Aberta;
    }

    private void DefinirCampos(
        Guid cartaoId,
        string competencia,
        DateOnly dataFechamento,
        DateOnly dataVencimento,
        decimal valorTotal,
        string? observacao)
    {
        if (cartaoId == Guid.Empty)
        {
            throw new ArgumentException("Cartao e obrigatorio.", nameof(cartaoId));
        }

        if (string.IsNullOrWhiteSpace(competencia))
        {
            throw new ArgumentException("Competencia e obrigatoria.", nameof(competencia));
        }

        if (valorTotal <= 0)
        {
            throw new ArgumentException("Valor total da fatura deve ser maior que zero.", nameof(valorTotal));
        }

        CartaoId = cartaoId;
        Competencia = competencia.Trim();
        DataFechamento = dataFechamento;
        DataVencimento = dataVencimento;
        ValorTotal = decimal.Round(valorTotal, 2, MidpointRounding.AwayFromZero);
        Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();
    }
}
