using ControleFinanceiro.SharedKernel.Common;
using FluentAssertions;

namespace ControleFinanceiro.Domain.Tests.Common;

public sealed class TenantEntityTests
{
    private sealed class FakeTenantEntity : TenantEntity;

    [Fact]
    public void AtribuirFamilia_Valida_DeveDefinirFamilia()
    {
        var entidade = new FakeTenantEntity();
        var familiaId = Guid.NewGuid();

        entidade.AtribuirFamilia(familiaId);

        entidade.FamiliaId.Should().Be(familiaId);
    }

    [Fact]
    public void AtribuirFamilia_MesmaFamilia_DeveSerIdempotente()
    {
        var entidade = new FakeTenantEntity();
        var familiaId = Guid.NewGuid();
        entidade.AtribuirFamilia(familiaId);

        entidade.AtribuirFamilia(familiaId);

        entidade.FamiliaId.Should().Be(familiaId);
    }

    [Fact]
    public void AtribuirFamilia_Vazia_DeveLancar()
    {
        var entidade = new FakeTenantEntity();

        var acao = () => entidade.AtribuirFamilia(Guid.Empty);

        acao.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AtribuirFamilia_OutraFamilia_DeveLancar()
    {
        var entidade = new FakeTenantEntity();
        entidade.AtribuirFamilia(Guid.NewGuid());

        var acao = () => entidade.AtribuirFamilia(Guid.NewGuid());

        acao.Should().Throw<InvalidOperationException>();
    }
}
