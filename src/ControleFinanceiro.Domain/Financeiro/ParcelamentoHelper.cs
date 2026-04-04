namespace ControleFinanceiro.Domain.Financeiro;

internal static class ParcelamentoHelper
{
    public static IReadOnlyCollection<decimal> Distribuir(decimal valorTotal, int quantidadeParcelas)
    {
        if (quantidadeParcelas < 1)
        {
            throw new ArgumentException("Quantidade de parcelas deve ser maior que zero.", nameof(quantidadeParcelas));
        }

        var totalEmCentavos = decimal.ToInt64(decimal.Round(valorTotal * 100m, 0, MidpointRounding.AwayFromZero));
        var baseEmCentavos = totalEmCentavos / quantidadeParcelas;
        var restanteEmCentavos = totalEmCentavos % quantidadeParcelas;
        var valores = new decimal[quantidadeParcelas];

        for (var index = 0; index < quantidadeParcelas; index++)
        {
            var centavos = baseEmCentavos + (index == quantidadeParcelas - 1 ? restanteEmCentavos : 0);
            valores[index] = centavos / 100m;
        }

        return valores;
    }

    public static IReadOnlyCollection<RateioPlano> DistribuirRateios(
        IReadOnlyCollection<RateioPlano> rateiosOriginais,
        decimal valorParcela,
        decimal valorTotal)
    {
        if (rateiosOriginais.Count == 1)
        {
            return [RateioPlano.Create(rateiosOriginais.Single().ContaGerencialId, valorParcela)];
        }

        var planos = new List<RateioPlano>(rateiosOriginais.Count);
        decimal acumulado = 0;

        foreach (var rateio in rateiosOriginais.Take(rateiosOriginais.Count - 1))
        {
            var valorDistribuido = decimal.Round(
                valorParcela * (rateio.Valor / valorTotal),
                2,
                MidpointRounding.AwayFromZero);

            acumulado += valorDistribuido;
            planos.Add(RateioPlano.Create(rateio.ContaGerencialId, valorDistribuido));
        }

        var ultimo = rateiosOriginais.Last();
        planos.Add(RateioPlano.Create(ultimo.ContaGerencialId, valorParcela - acumulado));

        return planos;
    }
}
