using ControleFinanceiro.Domain.Financeiro;
using FluentAssertions;

namespace ControleFinanceiro.Domain.Tests.Financeiro;

public sealed class RateioPlanoTests
{
    [Fact]
    public void Create_Valido_DeveArredondarValor()
    {
        var rateio = RateioPlano.Create(Guid.NewGuid(), 10.005m);

        rateio.Valor.Should().Be(10.01m);
    }

    [Fact]
    public void Create_ContaGerencialVazia_DeveLancar()
    {
        var acao = () => RateioPlano.Create(Guid.Empty, 10m);

        acao.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_ValorNaoPositivo_DeveLancar(decimal valor)
    {
        var acao = () => RateioPlano.Create(Guid.NewGuid(), valor);

        acao.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateSigned_ValorNegativo_DevePermitir()
    {
        var rateio = RateioPlano.CreateSigned(Guid.NewGuid(), -12.349m);

        rateio.Valor.Should().Be(-12.35m);
    }

    [Fact]
    public void CreateSigned_ContaGerencialVazia_DeveLancar()
    {
        var acao = () => RateioPlano.CreateSigned(Guid.Empty, 10m);

        acao.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateSigned_ValorZero_DeveLancar()
    {
        var acao = () => RateioPlano.CreateSigned(Guid.NewGuid(), 0m);

        acao.Should().Throw<ArgumentException>();
    }
}
