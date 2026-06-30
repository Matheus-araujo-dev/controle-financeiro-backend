using ControleFinanceiro.Domain.Cadastros.Cartoes;

namespace ControleFinanceiro.Domain.Tests.Cadastros;

public sealed class NumeroFinalCartaoTests
{
    [Theory]
    [InlineData("1234")]
    [InlineData("0000")]
    [InlineData("9")]
    public void De_DigitosValidos_CriaInstancia(string valor)
    {
        var vo = NumeroFinalCartao.De(valor);
        Assert.Equal(valor.Trim(), vo.Valor);
    }

    [Fact]
    public void De_VazioOuNulo_LancaExcecao()
    {
        Assert.Throws<ArgumentException>(() => NumeroFinalCartao.De(""));
        Assert.Throws<ArgumentException>(() => NumeroFinalCartao.De("   "));
    }

    [Fact]
    public void De_MaisDe4Digitos_LancaExcecao()
    {
        Assert.Throws<ArgumentException>(() => NumeroFinalCartao.De("12345"));
    }

    [Fact]
    public void De_CaracteresNaoNumericos_LancaExcecao()
    {
        Assert.Throws<ArgumentException>(() => NumeroFinalCartao.De("12AB"));
    }

    [Fact]
    public void Igualdade_MesmosValores()
    {
        Assert.Equal(NumeroFinalCartao.De("1234"), NumeroFinalCartao.De("1234"));
        Assert.True(NumeroFinalCartao.De("1234") == NumeroFinalCartao.De("1234"));
        Assert.True(NumeroFinalCartao.De("1234") != NumeroFinalCartao.De("5678"));
    }

    [Fact]
    public void ImplicitString_RetornaValor()
    {
        string valor = NumeroFinalCartao.De("4321");
        Assert.Equal("4321", valor);
    }

    [Fact]
    public void ToString_MascaraComAsteriscos()
    {
        Assert.Equal("**** 1234", NumeroFinalCartao.De("1234").ToString());
    }

    [Fact]
    public void TentarDe_NullOuVazio_RetornaNull()
    {
        Assert.Null(NumeroFinalCartao.TentarDe(null));
        Assert.Null(NumeroFinalCartao.TentarDe(""));
        Assert.Null(NumeroFinalCartao.TentarDe("XXXX"));
    }

    [Fact]
    public void TentarDe_ValorValido_RetornaInstancia()
    {
        var result = NumeroFinalCartao.TentarDe("9999");
        Assert.NotNull(result);
        Assert.Equal("9999", result!.Value.Valor);
    }
}
