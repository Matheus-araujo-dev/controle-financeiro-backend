namespace ControleFinanceiro.Domain.Financeiro;

/// <summary>
/// Cálculos monetários compartilhados entre <see cref="ContaPagar"/> e <see cref="ContaReceber"/>.
/// Centraliza a regra de arredondamento (2 casas, <see cref="MidpointRounding.AwayFromZero"/>)
/// para garantir consistência entre as duas entidades financeiras.
/// </summary>
public static class CalculoFinanceiro
{
    /// <summary>Valor líquido = original − desconto + juros + multa, arredondado a 2 casas.</summary>
    public static decimal ValorLiquido(decimal valorOriginal, decimal valorDesconto, decimal valorJuros, decimal valorMulta)
        => decimal.Round(valorOriginal - valorDesconto + valorJuros + valorMulta, 2, MidpointRounding.AwayFromZero);
}
