using ControleFinanceiro.Domain.Financeiro;
using FluentAssertions;
using Xunit;

namespace ControleFinanceiro.Domain.Tests.Financeiro;

public class InvestimentoTests
{
    private static readonly Guid ContaBancariaId = Guid.NewGuid();

    private static Investimento InvestimentoValido() =>
        Investimento.Criar("Tesouro SELIC 2027", "Tesouro Nacional", TipoInvestimento.RendaFixa,
            LiquidezInvestimento.Diaria, 10_000m, new DateOnly(2024, 1, 15),
            new DateOnly(2027, 3, 1), 11.75m, ContaBancariaId);

    [Fact]
    public void Criar_ComDadosValidos_DeveCriarInvestimento()
    {
        var inv = InvestimentoValido();

        inv.Nome.Should().Be("Tesouro SELIC 2027");
        inv.Emissor.Should().Be("Tesouro Nacional");
        inv.Tipo.Should().Be(TipoInvestimento.RendaFixa);
        inv.Liquidez.Should().Be(LiquidezInvestimento.Diaria);
        inv.ValorInvestido.Should().Be(10_000m);
        inv.ValorAtual.Should().Be(10_000m);
        inv.Rendimento.Should().Be(0m);
        inv.RendimentoPercent.Should().Be(0m);
        inv.Encerrado.Should().BeFalse();
        inv.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Criar_ComNomeTrimado_DeveSalvarSemEspacos()
    {
        var inv = Investimento.Criar("  CDB  ", null, TipoInvestimento.RendaFixa,
            LiquidezInvestimento.Vencimento, 5_000m, DateOnly.FromDateTime(DateTime.Today), null, null, ContaBancariaId);
        inv.Nome.Should().Be("CDB");
    }

    [Fact]
    public void Criar_ComEmissorVazio_DeveArmazenarNull()
    {
        var inv = Investimento.Criar("LCI", "   ", TipoInvestimento.RendaFixa,
            LiquidezInvestimento.Vencimento, 5_000m, DateOnly.FromDateTime(DateTime.Today), null, null, ContaBancariaId);
        inv.Emissor.Should().BeNull();
    }

    [Fact]
    public void Criar_ComNomeVazio_DeveLancarExcecao()
    {
        var acao = () => Investimento.Criar("", null, TipoInvestimento.RendaFixa,
            LiquidezInvestimento.Diaria, 1_000m, DateOnly.FromDateTime(DateTime.Today), null, null, ContaBancariaId);
        acao.Should().Throw<ArgumentException>().WithMessage("*Nome*");
    }

    [Fact]
    public void Criar_ComValorZero_DeveLancarExcecao()
    {
        var acao = () => Investimento.Criar("CDB", null, TipoInvestimento.RendaFixa,
            LiquidezInvestimento.Vencimento, 0m, DateOnly.FromDateTime(DateTime.Today), null, null, ContaBancariaId);
        acao.Should().Throw<ArgumentException>().WithMessage("*Valor investido*");
    }

    [Fact]
    public void Criar_ComContaBancariaVazia_DeveLancarExcecao()
    {
        var acao = () => Investimento.Criar("CDB", null, TipoInvestimento.RendaFixa,
            LiquidezInvestimento.Vencimento, 1_000m, DateOnly.FromDateTime(DateTime.Today), null, null, Guid.Empty);
        acao.Should().Throw<ArgumentException>().WithMessage("*Conta bancária*");
    }

    [Fact]
    public void Criar_ComTaxaNegativa_DeveLancarExcecao()
    {
        var acao = () => Investimento.Criar("CDB", null, TipoInvestimento.RendaFixa,
            LiquidezInvestimento.Vencimento, 1_000m, DateOnly.FromDateTime(DateTime.Today), null, -1m, ContaBancariaId);
        acao.Should().Throw<ArgumentException>().WithMessage("*Taxa anual*");
    }

    [Fact]
    public void AtualizarValorAtual_ComValorValido_DeveAtualizarRendimento()
    {
        var inv = InvestimentoValido();
        inv.AtualizarValorAtual(11_000m);

        inv.ValorAtual.Should().Be(11_000m);
        inv.Rendimento.Should().Be(1_000m);
        inv.RendimentoPercent.Should().Be(10m);
    }

    [Fact]
    public void AtualizarValorAtual_ComValorNegativo_DeveLancarExcecao()
    {
        var inv = InvestimentoValido();
        var acao = () => inv.AtualizarValorAtual(-100m);
        acao.Should().Throw<ArgumentException>().WithMessage("*Valor atual*");
    }

    [Fact]
    public void AtualizarValorAtual_QuandoEncerrado_DeveLancarExcecao()
    {
        var inv = InvestimentoValido();
        inv.Encerrar(10_500m);

        var acao = () => inv.AtualizarValorAtual(11_000m);
        acao.Should().Throw<InvalidOperationException>().WithMessage("*encerrado*");
    }

    [Fact]
    public void Encerrar_ComValorResgate_DeveMarcarComoEncerrado()
    {
        var inv = InvestimentoValido();
        inv.Encerrar(10_500m);

        inv.Encerrado.Should().BeTrue();
        inv.ValorAtual.Should().Be(10_500m);
        inv.Rendimento.Should().Be(500m);
    }

    [Fact]
    public void Encerrar_QuandoJaEncerrado_DeveLancarExcecao()
    {
        var inv = InvestimentoValido();
        inv.Encerrar(10_000m);

        var acao = () => inv.Encerrar(11_000m);
        acao.Should().Throw<InvalidOperationException>().WithMessage("*já está encerrado*");
    }

    [Fact]
    public void Encerrar_ComValorResgateNegativo_DeveLancarExcecao()
    {
        var inv = InvestimentoValido();
        var acao = () => inv.Encerrar(-1m);
        acao.Should().Throw<ArgumentException>().WithMessage("*Valor de resgate*");
    }

    [Fact]
    public void Atualizar_ComDadosValidos_DeveAtualizar()
    {
        var inv = InvestimentoValido();
        inv.Atualizar("CDB Premium", "Banco XP", TipoInvestimento.RendaFixa,
            LiquidezInvestimento.Vencimento, new DateOnly(2026, 12, 31), 13.5m);

        inv.Nome.Should().Be("CDB Premium");
        inv.Emissor.Should().Be("Banco XP");
        inv.TaxaAnual.Should().Be(13.5m);
        inv.DataVencimento.Should().Be(new DateOnly(2026, 12, 31));
    }

    [Fact]
    public void RendimentoPercent_SemValorInvestido_DeveRetornarZero()
    {
        // ValorInvestido é sempre > 0 por validação, mas testa a propriedade calculada
        var inv = InvestimentoValido();
        inv.AtualizarValorAtual(10_000m);
        inv.RendimentoPercent.Should().Be(0m);
    }
}
