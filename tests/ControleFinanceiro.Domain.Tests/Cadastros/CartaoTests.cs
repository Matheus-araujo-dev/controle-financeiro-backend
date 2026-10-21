using ControleFinanceiro.Domain.Cadastros.Cartoes;
using FluentAssertions;

namespace ControleFinanceiro.Domain.Tests.Cadastros;

public sealed class CartaoTests
{
    [Fact]
    public void Criar_QuandoNumeroFinalNaoPossuirQuatroDigitos_DeveFalhar()
    {
        var action = () => Cartao.Criar(
            "Cartao corporativo",
            "Visa",
            "12A4",
            8,
            15,
            null,
            5000m,
            true);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("numeroFinal");
    }

    [Fact]
    public void Criar_QuandoDiaFechamentoForaDoIntervalo_DeveFalhar()
    {
        var action = () => Cartao.Criar(
            "Cartao corporativo",
            "Visa",
            "1234",
            0,
            15,
            null,
            5000m,
            true);

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("diaFechamentoFatura");
    }

    [Fact]
    public void Criar_ComIconeECor_DeveArmazenarValoresNormalizados()
    {
        var cartao = Cartao.Criar(
            "Nubank",
            "Mastercard",
            "1234",
            3,
            10,
            null,
            null,
            true,
            icone: "  account_balance  ",
            cor: "  #8b5cf6  ");

        cartao.Icone.Should().Be("account_balance");
        cartao.Cor.Should().Be("#8b5cf6");
    }

    [Fact]
    public void Criar_ComIconeECorBrancos_DeveArmazenarNull()
    {
        var cartao = Cartao.Criar(
            "Inter",
            "Mastercard",
            "5678",
            10,
            20,
            null,
            null,
            true,
            icone: "   ",
            cor: "");

        cartao.Icone.Should().BeNull();
        cartao.Cor.Should().BeNull();
    }
}
