using ControleFinanceiro.Domain.Financeiro;
using FluentAssertions;

namespace ControleFinanceiro.Domain.Tests.Financeiro;

public sealed class FluxoCaixaDiarioTests
{
    [Fact]
    public void Projetar_DeveAcumularEntradasESaidasEDestacarSaldoNegativo()
    {
        var resultado = FluxoCaixaDiario.Projetar(
            new DateOnly(2026, 4, 5),
            dias: 3,
            saldoInicial: 100m,
            eventos:
            [
                new FluxoCaixaEvento(new DateOnly(2026, 4, 5), TipoMovimentacao.Saida, 80m),
                new FluxoCaixaEvento(new DateOnly(2026, 4, 6), TipoMovimentacao.Entrada, 30m),
                new FluxoCaixaEvento(new DateOnly(2026, 4, 6), TipoMovimentacao.Saida, 70m)
            ])
            .ToArray();

        resultado.Should().HaveCount(3);

        resultado[0].SaldoInicial.Should().Be(100m);
        resultado[0].EntradasPrevistas.Should().Be(0m);
        resultado[0].SaidasPrevistas.Should().Be(80m);
        resultado[0].SaldoFinalPrevisto.Should().Be(20m);
        resultado[0].RiscoSaldoNegativo.Should().BeFalse();

        resultado[1].SaldoInicial.Should().Be(20m);
        resultado[1].EntradasPrevistas.Should().Be(30m);
        resultado[1].SaidasPrevistas.Should().Be(70m);
        resultado[1].SaldoFinalPrevisto.Should().Be(-20m);
        resultado[1].RiscoSaldoNegativo.Should().BeTrue();

        resultado[2].SaldoInicial.Should().Be(-20m);
        resultado[2].SaldoFinalPrevisto.Should().Be(-20m);
        resultado[2].RiscoSaldoNegativo.Should().BeTrue();
    }

    [Fact]
    public void Projetar_DeveGerarDiasSemEventosMantendoSaldo()
    {
        var resultado = FluxoCaixaDiario.Projetar(
            new DateOnly(2026, 4, 10),
            dias: 2,
            saldoInicial: 350m,
            eventos: [])
            .ToArray();

        resultado.Should().SatisfyRespectively(
            primeiro =>
            {
                primeiro.Data.Should().Be(new DateOnly(2026, 4, 10));
                primeiro.SaldoInicial.Should().Be(350m);
                primeiro.EntradasPrevistas.Should().Be(0m);
                primeiro.SaidasPrevistas.Should().Be(0m);
                primeiro.SaldoFinalPrevisto.Should().Be(350m);
            },
            segundo =>
            {
                segundo.Data.Should().Be(new DateOnly(2026, 4, 11));
                segundo.SaldoInicial.Should().Be(350m);
                segundo.SaldoFinalPrevisto.Should().Be(350m);
            });
    }
}
