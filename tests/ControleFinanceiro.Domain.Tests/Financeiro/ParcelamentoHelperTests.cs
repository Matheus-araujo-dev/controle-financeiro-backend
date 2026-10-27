using ControleFinanceiro.Domain.Financeiro;
using FluentAssertions;
namespace ControleFinanceiro.Domain.Tests.Financeiro;
public sealed class ParcelamentoHelperTests
{
    [Fact]
    public void Distribuir_DivisaoExata_ParcelasIguais()
    {
        var resultado = ParcelamentoHelper.Distribuir(100.00m, 4);
        resultado.Should().HaveCount(4);
        resultado.Should().OnlyContain(v => v == 25.00m);
        resultado.Sum().Should().Be(100.00m);
    }

    [Fact]
    public void Distribuir_SobraDeCentavos_UltimaParcelaRecebe()
    {
        var resultado = ParcelamentoHelper.Distribuir(100.00m, 3);
        resultado.Should().HaveCount(3);
        resultado.ElementAt(0).Should().Be(33.33m);
        resultado.ElementAt(1).Should().Be(33.33m);
        resultado.ElementAt(2).Should().Be(33.34m);
        resultado.Sum().Should().Be(100.00m);
    }

    [Fact]
    public void Distribuir_ParcelaUnica_RetornaValorTotal()
    {
        var resultado = ParcelamentoHelper.Distribuir(448.65m, 1);
        resultado.Should().ContainSingle().Which.Should().Be(448.65m);
    }

    [Fact]
    public void Distribuir_QuantidadeZero_LancaExcecao()
    {
        var act = () => ParcelamentoHelper.Distribuir(100m, 0);
        act.Should().Throw<ArgumentException>().WithParameterName("quantidadeParcelas");
    }

    [Fact]
    public void Distribuir_QuantidadeNegativa_LancaExcecao()
    {
        var act = () => ParcelamentoHelper.Distribuir(100m, -1);
        act.Should().Throw<ArgumentException>().WithParameterName("quantidadeParcelas");
    }

    [Fact]
    public void Distribuir_ValorPequeno_MuitasParcelas()
    {
        var resultado = ParcelamentoHelper.Distribuir(0.10m, 3);
        resultado.Should().HaveCount(3);
        resultado.Sum().Should().Be(0.10m);
    }

    [Fact]
    public void DistribuirRateios_RateioUnico_RetornaValorDaParcela()
    {
        var contaId = Guid.NewGuid();
        var rateios = new[] { RateioPlano.Create(contaId, 100m) };
        var resultado = ParcelamentoHelper.DistribuirRateios(rateios, 50m, 100m);
        resultado.Should().ContainSingle();
        resultado.Single().ContaGerencialId.Should().Be(contaId);
        resultado.Single().Valor.Should().Be(50m);
    }

    [Fact]
    public void DistribuirRateios_DoisRateios_DistribuiProporcional()
    {
        var conta1 = Guid.NewGuid();
        var conta2 = Guid.NewGuid();
        var rateios = new[] { RateioPlano.Create(conta1, 70m), RateioPlano.Create(conta2, 30m) };
        var resultado = ParcelamentoHelper.DistribuirRateios(rateios, 50m, 100m);
        resultado.Should().HaveCount(2);
        resultado.ElementAt(0).ContaGerencialId.Should().Be(conta1);
        resultado.ElementAt(1).ContaGerencialId.Should().Be(conta2);
        resultado.Sum(r => r.Valor).Should().Be(50m);
    }

    [Fact]
    public void DistribuirRateios_TresRateios_UltimoAbsorveArredondamento()
    {
        var c1 = Guid.NewGuid();
        var c2 = Guid.NewGuid();
        var c3 = Guid.NewGuid();
        var rateios = new[] { RateioPlano.Create(c1, 33.33m), RateioPlano.Create(c2, 33.33m), RateioPlano.Create(c3, 33.34m) };
        var resultado = ParcelamentoHelper.DistribuirRateios(rateios, 50m, 100m);
        resultado.Should().HaveCount(3);
        resultado.Sum(r => r.Valor).Should().Be(50m);
    }
}
