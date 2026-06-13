using ControleFinanceiro.Domain.FinanceAI;
using FluentAssertions;

namespace ControleFinanceiro.Application.Tests.FinanceAI;

public sealed class WhatsappConfigAlertaDomainTests
{
    private static readonly Guid FamiliaId = Guid.NewGuid();
    private static readonly Guid UsuarioId = Guid.NewGuid();

    [Fact]
    public void CriarPadrao_DeveRetornarConfigComVencimentoAtivo3Dias()
    {
        var cfg = WhatsappConfigAlerta.CriarPadrao(FamiliaId, UsuarioId);

        cfg.ReceberVencimento.Should().BeTrue();
        cfg.DiasAntecedenciaVencimento.Should().Be(3);
        cfg.ReceberLimiteCategoria.Should().BeFalse();
        cfg.ReceberLimiteResponsavel.Should().BeFalse();
        cfg.FamiliaId.Should().Be(FamiliaId);
        cfg.UsuarioId.Should().Be(UsuarioId);
    }

    [Theory]
    [InlineData(0, 1)]   // abaixo do mínimo → clamp para 1
    [InlineData(1, 1)]
    [InlineData(15, 15)]
    [InlineData(30, 30)]
    [InlineData(31, 30)]  // acima do máximo → clamp para 30
    [InlineData(100, 30)]
    public void Atualizar_DeveClamparDiasAntecedencia(int dias, int esperado)
    {
        var cfg = WhatsappConfigAlerta.CriarPadrao(FamiliaId, UsuarioId);

        cfg.Atualizar(true, dias, false, false);

        cfg.DiasAntecedenciaVencimento.Should().Be(esperado);
    }

    [Fact]
    public void Atualizar_DeveAtualizarTodosOsCampos()
    {
        var cfg = WhatsappConfigAlerta.CriarPadrao(FamiliaId, UsuarioId);

        cfg.Atualizar(false, 7, true, true);

        cfg.ReceberVencimento.Should().BeFalse();
        cfg.DiasAntecedenciaVencimento.Should().Be(7);
        cfg.ReceberLimiteCategoria.Should().BeTrue();
        cfg.ReceberLimiteResponsavel.Should().BeTrue();
    }
}
