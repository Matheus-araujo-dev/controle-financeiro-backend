using ControleFinanceiro.Domain.Financeiro;
using FluentAssertions;

namespace ControleFinanceiro.Domain.Tests.Financeiro;

public sealed class AjusteConciliacaoFaturaTests
{
    private static ContaPagar Criar(decimal valor, Guid? cartao = null) => ContaPagar.Criar(
        null, new DateOnly(2026, 9, 1), Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 9, 20),
        Guid.NewGuid(), cartao, null, valor, 0, 0, 0, 3, 2, Guid.NewGuid(), null,
        "Compra 2/3", null, StatusConta.PendenteId, false, null, OrigemLancamento.Manual,
        [RateioPlano.CreateSigned(Guid.NewGuid(), valor)]);

    [Theory]
    [InlineData(33.34, 33.33)]
    [InlineData(-33.34, -33.33)]
    public void Ajustar_PreservaVinculosEFechaRateios(decimal anterior, decimal novo)
    {
        var conta = Criar(anterior, Guid.NewGuid());
        var grupo = conta.GrupoParcelamentoId;
        var categoria = conta.Rateios.Single().ContaGerencialId;
        conta.AjustarValorConciliacaoFatura(novo, [RateioPlano.CreateSigned(categoria, novo)]);
        conta.ValorLiquido.Should().Be(novo);
        conta.ValorOriginal.Should().Be(novo);
        conta.Rateios.Sum(x => x.Valor).Should().Be(novo);
        conta.GrupoParcelamentoId.Should().Be(grupo);
        conta.NumeroParcela.Should().Be(2);
        conta.QuantidadeParcelas.Should().Be(3);
        conta.Descricao.Should().Be("Compra 2/3");
    }

    [Fact]
    public void Ajustar_RateioInvalidoNaoAlteraConta()
    {
        var conta = Criar(33.34m, Guid.NewGuid());
        var act = () => conta.AjustarValorConciliacaoFatura(33.33m,
            [RateioPlano.Create(Guid.NewGuid(), 30m)]);
        act.Should().Throw<ArgumentException>();
        conta.ValorLiquido.Should().Be(33.34m);
        conta.Rateios.Sum(x => x.Valor).Should().Be(33.34m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-33.33)]
    [InlineData(33.333)]
    public void Ajustar_RejeitaZeroTrocaDeSinalOuFracaoDeCentavo(decimal novo)
    {
        var conta = Criar(33.34m, Guid.NewGuid());
        var act = () => conta.AjustarValorConciliacaoFatura(novo, []);
        act.Should().Throw<ArgumentException>();
        conta.ValorLiquido.Should().Be(33.34m);
    }

    [Fact]
    public void Ajustar_NaoAceitaContaForaDoCartao()
    {
        var conta = Criar(33.34m);
        var act = () => conta.AjustarValorConciliacaoFatura(33.33m, []);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Ajustar_NaoAceitaContaLiquidada()
    {
        var conta = Criar(33.34m, Guid.NewGuid());
        conta.Liquidar(new DateOnly(2026, 9, 20), Guid.NewGuid(), StatusConta.LiquidadaId);
        var act = () => conta.AjustarValorConciliacaoFatura(33.33m, []);
        act.Should().Throw<InvalidOperationException>();
    }
}
