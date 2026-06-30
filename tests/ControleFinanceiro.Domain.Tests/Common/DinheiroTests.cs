using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.Tests.Common;

public sealed class DinheiroTests
{
    [Fact]
    public void De_ValorPositivo_CriaInstancia()
    {
        var d = Dinheiro.De(100.50m);
        Assert.Equal(100.50m, d.Valor);
    }

    [Fact]
    public void De_ValorNegativo_LancaExcecao()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Dinheiro.De(-0.01m));
    }

    [Fact]
    public void De_ArredondaDuasCasas()
    {
        var d = Dinheiro.De(1.005m);
        Assert.Equal(1.01m, d.Valor);
    }

    [Fact]
    public void Zero_RetornaValorZero()
    {
        Assert.Equal(0m, Dinheiro.Zero.Valor);
    }

    [Fact]
    public void Soma_DoisValores()
    {
        var resultado = Dinheiro.De(10m) + Dinheiro.De(5.50m);
        Assert.Equal(15.50m, resultado.Valor);
    }

    [Fact]
    public void Subtracao_RetornaValorNegativoPermitido()
    {
        var resultado = Dinheiro.De(5m) - Dinheiro.De(10m);
        Assert.Equal(-5m, resultado.Valor);
    }

    [Fact]
    public void Multiplicacao_PorFator()
    {
        var resultado = Dinheiro.De(100m) * 1.1m;
        Assert.Equal(110m, resultado.Valor);
    }

    [Fact]
    public void Igualdade_MesmosValores()
    {
        Assert.Equal(Dinheiro.De(50m), Dinheiro.De(50m));
        Assert.True(Dinheiro.De(50m) == Dinheiro.De(50m));
    }

    [Fact]
    public void Comparacao_Ordenacao()
    {
        Assert.True(Dinheiro.De(10m) > Dinheiro.De(5m));
        Assert.True(Dinheiro.De(5m) < Dinheiro.De(10m));
    }

    [Fact]
    public void ImplicitDecimal_RetornaValor()
    {
        decimal valor = Dinheiro.De(99.99m);
        Assert.Equal(99.99m, valor);
    }

    [Fact]
    public void ToString_FormataN2()
    {
        Assert.Equal("1.000,50", Dinheiro.De(1000.50m).ToString());
    }
}
