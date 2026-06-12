using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.Financeiro;

public sealed class RateioContaGerencial : TenantEntity
{
    private RateioContaGerencial()
    {
    }

    public TipoLancamentoRateio TipoLancamento { get; private set; }

    public Guid? ContaPagarId { get; private set; }

    public Guid? ContaReceberId { get; private set; }

    public Guid ContaGerencialId { get; private set; }

    public decimal? Percentual { get; private set; }

    public decimal Valor { get; private set; }

    public static RateioContaGerencial CriarContaPagar(Guid contaPagarId, RateioPlano plano, decimal valorBase)
    {
        return Criar(TipoLancamentoRateio.ContaPagar, contaPagarId, null, plano, valorBase);
    }

    public static RateioContaGerencial CriarContaReceber(Guid contaReceberId, RateioPlano plano, decimal valorBase)
    {
        return Criar(TipoLancamentoRateio.ContaReceber, null, contaReceberId, plano, valorBase);
    }

    private static RateioContaGerencial Criar(
        TipoLancamentoRateio tipoLancamento,
        Guid? contaPagarId,
        Guid? contaReceberId,
        RateioPlano plano,
        decimal valorBase)
    {
        if (valorBase == 0)
        {
            throw new ArgumentException("Valor base deve ser diferente de zero.", nameof(valorBase));
        }

        var percentual = decimal.Round((plano.Valor / valorBase) * 100m, 4, MidpointRounding.AwayFromZero);

        return new RateioContaGerencial
        {
            TipoLancamento = tipoLancamento,
            ContaPagarId = contaPagarId,
            ContaReceberId = contaReceberId,
            ContaGerencialId = plano.ContaGerencialId,
            Valor = plano.Valor,
            Percentual = percentual
        };
    }
}
