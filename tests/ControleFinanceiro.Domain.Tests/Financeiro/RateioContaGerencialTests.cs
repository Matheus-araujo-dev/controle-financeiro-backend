using ControleFinanceiro.Domain.Financeiro;
using FluentAssertions;

namespace ControleFinanceiro.Domain.Tests.Financeiro;

public sealed class RateioContaGerencialTests
{
    [Fact]
    public void CriarContaPagar_ComPlanoValido_CriaRateioComPercentual()
    {
        var contaPagarId = Guid.NewGuid();
        var contaGerencialId = Guid.NewGuid();
        var plano = RateioPlano.Create(contaGerencialId, 50m);

        var rateio = RateioContaGerencial.CriarContaPagar(contaPagarId, plano, 200m);

        rateio.ContaPagarId.Should().Be(contaPagarId);
        rateio.ContaReceberId.Should().BeNull();
        rateio.ContaGerencialId.Should().Be(contaGerencialId);
        rateio.Valor.Should().Be(50m);
        rateio.Percentual.Should().Be(25m);
        rateio.TipoLancamento.Should().Be(TipoLancamentoRateio.ContaPagar);
    }

    [Fact]
    public void CriarContaReceber_ComPlanoValido_CriaRateioComPercentual()
    {
        var contaReceberId = Guid.NewGuid();
        var contaGerencialId = Guid.NewGuid();
        var plano = RateioPlano.Create(contaGerencialId, 30m);

        var rateio = RateioContaGerencial.CriarContaReceber(contaReceberId, plano, 100m);

        rateio.ContaReceberId.Should().Be(contaReceberId);
        rateio.ContaPagarId.Should().BeNull();
        rateio.ContaGerencialId.Should().Be(contaGerencialId);
        rateio.Valor.Should().Be(30m);
        rateio.Percentual.Should().Be(30m);
        rateio.TipoLancamento.Should().Be(TipoLancamentoRateio.ContaReceber);
    }

    [Fact]
    public void CriarContaPagar_ValorBaseZero_LancaExcecao()
    {
        var plano = RateioPlano.Create(Guid.NewGuid(), 50m);
        var act = () => RateioContaGerencial.CriarContaPagar(Guid.NewGuid(), plano, 0m);
        act.Should().Throw<ArgumentException>().WithMessage("*Valor base*");
    }

    [Fact]
    public void CriarContaReceber_ValorBaseZero_LancaExcecao()
    {
        var plano = RateioPlano.Create(Guid.NewGuid(), 50m);
        var act = () => RateioContaGerencial.CriarContaReceber(Guid.NewGuid(), plano, 0m);
        act.Should().Throw<ArgumentException>().WithMessage("*Valor base*");
    }

    [Fact]
    public void CriarContaPagar_RateioIntegral_Percentual100()
    {
        var plano = RateioPlano.Create(Guid.NewGuid(), 250m);
        var rateio = RateioContaGerencial.CriarContaPagar(Guid.NewGuid(), plano, 250m);
        rateio.Percentual.Should().Be(100m);
    }

    [Fact]
    public void CriarContaPagar_PercentualComDecimais_ArredondaQuatroCasas()
    {
        var plano = RateioPlano.Create(Guid.NewGuid(), 33.33m);
        var rateio = RateioContaGerencial.CriarContaPagar(Guid.NewGuid(), plano, 100m);
        rateio.Percentual.Should().Be(33.33m);
    }

    [Fact]
    public void CriarContaPagar_PercentualTerco_ArredondaCorretamente()
    {
        var plano = RateioPlano.Create(Guid.NewGuid(), 1m);
        var rateio = RateioContaGerencial.CriarContaPagar(Guid.NewGuid(), plano, 3m);
        rateio.Percentual.Should().Be(33.3333m);
    }
}
