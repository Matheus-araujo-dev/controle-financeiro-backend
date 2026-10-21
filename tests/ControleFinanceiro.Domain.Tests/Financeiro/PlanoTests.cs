using ControleFinanceiro.Domain.Financeiro;
using FluentAssertions;
using Xunit;

namespace ControleFinanceiro.Domain.Tests.Financeiro;

public class PlanoTests
{
    private static readonly Guid ContaBancariaId = Guid.NewGuid();

    private static Plano PlanoValido() =>
        Plano.Criar("Reserva de emergência", "Meta 6 meses", 500m, 12, ContaBancariaId);

    [Fact]
    public void Criar_ComDadosValidos_DeveCriarPlano()
    {
        var plano = PlanoValido();

        plano.Nome.Should().Be("Reserva de emergência");
        plano.Descricao.Should().Be("Meta 6 meses");
        plano.ValorMensal.Should().Be(500m);
        plano.NumParcelas.Should().Be(12);
        plano.ContaBancariaCaixaId.Should().Be(ContaBancariaId);
        plano.ParcelasPagas.Should().Be(0);
        plano.TotalRetirado.Should().Be(0m);
        plano.Cancelado.Should().BeFalse();
        plano.Concluido.Should().BeFalse();
        plano.ValorTotal.Should().Be(6000m);
        plano.TotalAcumulado.Should().Be(0m);
        plano.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Criar_ComNomeComEspacos_DeveTrimmar()
    {
        var plano = Plano.Criar("  Férias  ", null, 200m, 6, ContaBancariaId);
        plano.Nome.Should().Be("Férias");
    }

    [Fact]
    public void Criar_ComDescricaoVazia_DeveArmazenarNull()
    {
        var plano = Plano.Criar("Viagem", "   ", 200m, 6, ContaBancariaId);
        plano.Descricao.Should().BeNull();
    }

    [Fact]
    public void Criar_ComNomeVazio_DeveLancarExcecao()
    {
        var acao = () => Plano.Criar("", null, 200m, 6, ContaBancariaId);
        acao.Should().Throw<ArgumentException>().WithMessage("*Nome*");
    }

    [Fact]
    public void Criar_ComValorMensalZero_DeveLancarExcecao()
    {
        var acao = () => Plano.Criar("Plano", null, 0m, 6, ContaBancariaId);
        acao.Should().Throw<ArgumentException>().WithMessage("*Valor mensal*");
    }

    [Fact]
    public void Criar_ComValorMensalNegativo_DeveLancarExcecao()
    {
        var acao = () => Plano.Criar("Plano", null, -100m, 6, ContaBancariaId);
        acao.Should().Throw<ArgumentException>().WithMessage("*Valor mensal*");
    }

    [Fact]
    public void Criar_ComNumParcelasZero_DeveLancarExcecao()
    {
        var acao = () => Plano.Criar("Plano", null, 100m, 0, ContaBancariaId);
        acao.Should().Throw<ArgumentException>().WithMessage("*parcelas*");
    }

    [Fact]
    public void Criar_ComContaBancariaVazia_DeveLancarExcecao()
    {
        var acao = () => Plano.Criar("Plano", null, 100m, 6, Guid.Empty);
        acao.Should().Throw<ArgumentException>().WithMessage("*Conta bancária*");
    }

    [Fact]
    public void AdiantarParcela_DeveincrementarParcelasPagas()
    {
        var plano = PlanoValido();
        plano.AdiantarParcela();
        plano.ParcelasPagas.Should().Be(1);
        plano.TotalAcumulado.Should().Be(500m);
    }

    [Fact]
    public void AdiantarParcela_AteLimite_DeveMarcarComoConcluido()
    {
        var plano = Plano.Criar("Plano", null, 100m, 2, ContaBancariaId);
        plano.AdiantarParcela();
        plano.AdiantarParcela();

        plano.ParcelasPagas.Should().Be(2);
        plano.Concluido.Should().BeTrue();
    }

    [Fact]
    public void AdiantarParcela_QuandoConcluido_DeveLancarExcecao()
    {
        var plano = Plano.Criar("Plano", null, 100m, 1, ContaBancariaId);
        plano.AdiantarParcela();

        var acao = () => plano.AdiantarParcela();
        acao.Should().Throw<InvalidOperationException>().WithMessage("*concluído*");
    }

    [Fact]
    public void AdiantarParcela_QuandoCancelado_DeveLancarExcecao()
    {
        var plano = PlanoValido();
        plano.Cancelar();

        var acao = () => plano.AdiantarParcela();
        acao.Should().Throw<InvalidOperationException>().WithMessage("*cancelado*");
    }

    [Fact]
    public void RetirarDinheiro_ComValorValido_DeveAtualizarTotalRetirado()
    {
        var plano = PlanoValido();
        plano.AdiantarParcela();
        plano.RetirarDinheiro(200m);

        plano.TotalRetirado.Should().Be(200m);
        plano.TotalAcumulado.Should().Be(300m);
    }

    [Fact]
    public void RetirarDinheiro_ExcedendoAcumulado_DeveLancarExcecao()
    {
        var plano = PlanoValido();
        plano.AdiantarParcela();

        var acao = () => plano.RetirarDinheiro(600m);
        acao.Should().Throw<InvalidOperationException>().WithMessage("*excede*");
    }

    [Fact]
    public void RetirarDinheiro_ComValorZero_DeveLancarExcecao()
    {
        var plano = PlanoValido();
        var acao = () => plano.RetirarDinheiro(0m);
        acao.Should().Throw<ArgumentException>().WithMessage("*Valor*");
    }

    [Fact]
    public void RetirarDinheiro_QuandoCancelado_DeveLancarExcecao()
    {
        var plano = PlanoValido();
        plano.Cancelar();

        var acao = () => plano.RetirarDinheiro(100m);
        acao.Should().Throw<InvalidOperationException>().WithMessage("*cancelado*");
    }

    [Fact]
    public void Cancelar_DeveMarcarComoCancelado()
    {
        var plano = PlanoValido();
        plano.Cancelar();
        plano.Cancelado.Should().BeTrue();
    }

    [Fact]
    public void Cancelar_QuandoJaCancelado_DeveLancarExcecao()
    {
        var plano = PlanoValido();
        plano.Cancelar();

        var acao = () => plano.Cancelar();
        acao.Should().Throw<InvalidOperationException>().WithMessage("*já está cancelado*");
    }

    [Fact]
    public void Atualizar_ComDadosValidos_DeveAtualizar()
    {
        var plano = PlanoValido();
        plano.Atualizar("Novo nome", "Nova desc", 1000m, 24);

        plano.Nome.Should().Be("Novo nome");
        plano.Descricao.Should().Be("Nova desc");
        plano.ValorMensal.Should().Be(1000m);
        plano.NumParcelas.Should().Be(24);
        plano.ValorTotal.Should().Be(24000m);
    }

    [Fact]
    public void Atualizar_ComNumParcelasMenorQueParcelasPagas_DeveLancarExcecao()
    {
        var plano = PlanoValido();
        plano.AdiantarParcela();
        plano.AdiantarParcela();

        var acao = () => plano.Atualizar("Plano", null, 500m, 1);
        acao.Should().Throw<ArgumentException>().WithMessage("*menor que as parcelas já pagas*");
    }
}
