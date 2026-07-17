using ControleFinanceiro.Domain.Financeiro;
using FluentAssertions;

namespace ControleFinanceiro.Domain.Tests.Financeiro;

public sealed class MetaOrcamentoTests
{
    [Fact]
    public void Criar_ComDadosValidos_DeveDefinirPropriedades()
    {
        var contaGerencialId = Guid.NewGuid();
        var meta = MetaOrcamento.Criar(contaGerencialId, "2026-07", 1000m);

        meta.ContaGerencialId.Should().Be(contaGerencialId);
        meta.Competencia.Should().Be("2026-07");
        meta.ValorMeta.Should().Be(1000m);
    }

    [Fact]
    public void Criar_ComContaGerencialVazia_DeveLancarExcecao()
    {
        var act = () => MetaOrcamento.Criar(Guid.Empty, "2026-07", 1000m);

        act.Should().Throw<ArgumentException>().WithMessage("*gerencial*");
    }

    [Fact]
    public void Criar_ComCompetenciaVazia_DeveLancarExcecao()
    {
        var act = () => MetaOrcamento.Criar(Guid.NewGuid(), "", 1000m);

        act.Should().Throw<ArgumentException>().WithMessage("*ompetência*");
    }

    [Fact]
    public void Criar_ComCompetenciaFormatoInvalido_DeveLancarExcecao()
    {
        var act = () => MetaOrcamento.Criar(Guid.NewGuid(), "07-2026", 1000m);

        act.Should().Throw<ArgumentException>().WithMessage("*formato*");
    }

    [Fact]
    public void Atualizar_ComValorZero_DeveLancarExcecao()
    {
        var meta = MetaOrcamento.Criar(Guid.NewGuid(), "2026-07", 500m);

        var act = () => meta.Atualizar(0m);

        act.Should().Throw<ArgumentException>().WithMessage("*maior que zero*");
    }

    [Fact]
    public void Atualizar_ComValorValido_DeveAtualizarMeta()
    {
        var meta = MetaOrcamento.Criar(Guid.NewGuid(), "2026-07", 500m);

        meta.Atualizar(800m);

        meta.ValorMeta.Should().Be(800m);
    }
}
