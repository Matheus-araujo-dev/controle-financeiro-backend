namespace ControleFinanceiro.Domain.Financeiro;

public sealed record RateioPlano(Guid ContaGerencialId, decimal Valor)
{
    public static RateioPlano Create(Guid contaGerencialId, decimal valor)
    {
        if (contaGerencialId == Guid.Empty)
        {
            throw new ArgumentException("Conta gerencial e obrigatoria.", nameof(contaGerencialId));
        }

        if (valor <= 0)
        {
            throw new ArgumentException("Valor do rateio deve ser maior que zero.", nameof(valor));
        }

        return new RateioPlano(contaGerencialId, decimal.Round(valor, 2, MidpointRounding.AwayFromZero));
    }
}
