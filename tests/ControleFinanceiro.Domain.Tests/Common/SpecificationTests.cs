using ControleFinanceiro.Domain.Financeiro;
using ControleFinanceiro.Domain.Financeiro.Specifications;
using ControleFinanceiro.Domain.Tests.Builders;

namespace ControleFinanceiro.Domain.Tests.Common;

public sealed class SpecificationTests
{
    private static ContaPagar Build(
        DateOnly? vencimento = null,
        Guid? cartaoId = null,
        decimal valor = 100m,
        Guid? statusId = null)
    {
        var builder = new ContaPagarBuilder()
            .ComDataVencimento(vencimento ?? DateOnly.FromDateTime(DateTime.Today))
            .ComCartaoId(cartaoId)
            .ComValorOriginal(valor);

        if (statusId.HasValue) builder = builder.ComStatusContaId(statusId.Value);

        return builder.Build();
    }

    [Fact]
    public void ContaPagarPendentesSpec_ContaPendente_Satisfaz()
    {
        var conta = Build(statusId: StatusConta.PendenteId);
        Assert.True(new ContaPagarPendentesSpec().IsSatisfiedBy(conta));
    }

    [Fact]
    public void ContaPagarPendentesSpec_ContaLiquidada_NaoSatisfaz()
    {
        var conta = Build(statusId: StatusConta.LiquidadaId);
        Assert.False(new ContaPagarPendentesSpec().IsSatisfiedBy(conta));
    }

    [Fact]
    public void ContaPagarNaoCartaoSpec_SemCartao_Satisfaz()
    {
        var conta = Build();
        Assert.True(new ContaPagarNaoCartaoSpec().IsSatisfiedBy(conta));
    }

    [Fact]
    public void ContaPagarNaoCartaoSpec_ComCartao_NaoSatisfaz()
    {
        var conta = Build(cartaoId: Guid.NewGuid());
        Assert.False(new ContaPagarNaoCartaoSpec().IsSatisfiedBy(conta));
    }

    [Fact]
    public void ContaPagarPorVencimentoAteSpec_DataAnterior_Satisfaz()
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var conta = Build(vencimento: hoje.AddDays(-1));
        Assert.True(new ContaPagarPorVencimentoAteSpec(hoje).IsSatisfiedBy(conta));
    }

    [Fact]
    public void ContaPagarPorVencimentoAteSpec_DataPosterior_NaoSatisfaz()
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var conta = Build(vencimento: hoje.AddDays(1));
        Assert.False(new ContaPagarPorVencimentoAteSpec(hoje).IsSatisfiedBy(conta));
    }

    [Fact]
    public void AndSpec_AmbasSatisfeitas_Satisfaz()
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var conta = Build(vencimento: hoje);
        var spec = new ContaPagarNaoCartaoSpec() & new ContaPagarPorVencimentoAteSpec(hoje);
        Assert.True(spec.IsSatisfiedBy(conta));
    }

    [Fact]
    public void AndSpec_UmaNaoSatisfeita_NaoSatisfaz()
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var contaComCartao = Build(vencimento: hoje, cartaoId: Guid.NewGuid());
        var spec = new ContaPagarNaoCartaoSpec() & new ContaPagarPorVencimentoAteSpec(hoje);
        Assert.False(spec.IsSatisfiedBy(contaComCartao));
    }

    [Fact]
    public void OrSpec_PeloMenosUmaSatisfeita_Satisfaz()
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var contaGrande = Build(vencimento: hoje.AddDays(5), valor: 500m);
        var spec = new ContaPagarPorVencimentoAteSpec(hoje) | new ContaPagarPorValorMinimoSpec(300m);
        Assert.True(spec.IsSatisfiedBy(contaGrande));
    }

    [Fact]
    public void NotSpec_NegaResultado()
    {
        var conta = Build(cartaoId: Guid.NewGuid());
        var spec = !new ContaPagarNaoCartaoSpec();
        Assert.True(spec.IsSatisfiedBy(conta));
    }

    [Fact]
    public void Apply_FiltraQueryable()
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var contas = new[]
        {
            Build(valor: 50m),
            Build(valor: 200m),
            Build(valor: 100m)
        }.AsQueryable();

        var resultado = new ContaPagarPorValorMinimoSpec(100m).Apply(contas).ToList();
        Assert.Equal(2, resultado.Count);
    }
}
