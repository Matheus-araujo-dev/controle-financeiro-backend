namespace ControleFinanceiro.Domain.Financeiro;

public sealed record FluxoCaixaEvento(DateOnly Data, TipoMovimentacao Tipo, decimal Valor);

public sealed record FluxoCaixaDia(
    DateOnly Data,
    decimal SaldoInicial,
    decimal EntradasPrevistas,
    decimal SaidasPrevistas,
    decimal SaldoFinalPrevisto,
    bool RiscoSaldoNegativo);

public static class FluxoCaixaDiario
{
    public static IReadOnlyCollection<FluxoCaixaDia> Projetar(
        DateOnly dataInicial,
        int dias,
        decimal saldoInicial,
        IReadOnlyCollection<FluxoCaixaEvento> eventos)
    {
        if (dias < 1)
        {
            throw new ArgumentException("Quantidade de dias deve ser maior que zero.", nameof(dias));
        }

        var eventosPorDia = eventos
            .GroupBy(x => x.Data)
            .ToDictionary(
                grupo => grupo.Key,
                grupo => new
                {
                    Entradas = decimal.Round(
                        grupo.Where(x => x.Tipo == TipoMovimentacao.Entrada).Sum(x => x.Valor),
                        2,
                        MidpointRounding.AwayFromZero),
                    Saidas = decimal.Round(
                        grupo.Where(x => x.Tipo == TipoMovimentacao.Saida).Sum(x => x.Valor),
                        2,
                        MidpointRounding.AwayFromZero)
                });

        var itens = new List<FluxoCaixaDia>(dias);
        var saldoCorrente = decimal.Round(saldoInicial, 2, MidpointRounding.AwayFromZero);

        for (var indice = 0; indice < dias; indice++)
        {
            var data = dataInicial.AddDays(indice);
            var movimentoDia = eventosPorDia.GetValueOrDefault(data);
            var entradas = movimentoDia?.Entradas ?? 0m;
            var saidas = movimentoDia?.Saidas ?? 0m;
            var saldoFinal = decimal.Round(saldoCorrente + entradas - saidas, 2, MidpointRounding.AwayFromZero);

            itens.Add(new FluxoCaixaDia(
                data,
                saldoCorrente,
                entradas,
                saidas,
                saldoFinal,
                saldoFinal < 0));

            saldoCorrente = saldoFinal;
        }

        return itens;
    }
}
