using ControleFinanceiro.Domain.Financeiro;
using FluentAssertions;

namespace ControleFinanceiro.Domain.Tests.Financeiro;

public sealed class CalculoFinanceiroTests
{
    [Fact]
    public void ValorLiquido_SemDescontoNemAcrescimo_RetornaOriginal()
    {
        CalculoFinanceiro.ValorLiquido(100m, 0m, 0m, 0m).Should().Be(100m);
    }

    [Fact]
    public void ValorLiquido_ComDescontoJurosMulta_CalculaCorretamente()
    {
        CalculoFinanceiro.ValorLiquido(100m, 10m, 5m, 3m).Should().Be(98m);
    }

    [Fact]
    public void ValorLiquido_ArredondaDuasCasas_AwayFromZero()
    {
        CalculoFinanceiro.ValorLiquido(100m, 0m, 0.005m, 0m).Should().Be(100.01m);
    }

    [Fact]
    public void ValorLiquido_DescontoMaiorQueOriginal_RetornaNegativo()
    {
        CalculoFinanceiro.ValorLiquido(50m, 80m, 0m, 0m).Should().Be(-30m);
    }

    [Fact]
    public void ValorLiquido_TodosZeros_RetornaZero()
    {
        CalculoFinanceiro.ValorLiquido(0m, 0m, 0m, 0m).Should().Be(0m);
    }
}
