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
}
