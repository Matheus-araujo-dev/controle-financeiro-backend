using ControleFinanceiro.Domain.FinanceAI;
using FluentAssertions;
using Xunit;

namespace ControleFinanceiro.Domain.Tests.FinanceAI;

public class ConfiguracaoNotificacaoTests
{
    private static readonly Guid FamiliaId = Guid.NewGuid();
    private static readonly Guid UsuarioId = Guid.NewGuid();

    private static ConfiguracaoNotificacao ConfigPadrao() =>
        ConfiguracaoNotificacao.CriarPadrao(FamiliaId, UsuarioId);

    [Fact]
    public void CriarPadrao_DeveDefinirValoresCorretos()
    {
        var config = ConfigPadrao();

        config.UsuarioId.Should().Be(UsuarioId);
        config.FamiliaId.Should().Be(FamiliaId);
        config.EmailAtivo.Should().BeFalse();
        config.EmailDestinatario.Should().BeNull();
        config.EmailVencimento.Should().BeTrue();
        config.EmailDiasAntecedencia.Should().Be(3);
        config.EmailLimiteCategoria.Should().BeFalse();
        config.PushAtivo.Should().BeFalse();
        config.PushVencimento.Should().BeTrue();
        config.PushDiasAntecedencia.Should().Be(1);
        config.PushLimiteCategoria.Should().BeFalse();
        config.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Atualizar_DeveAplicarNovosValores()
    {
        var config = ConfigPadrao();

        config.Atualizar(
            emailAtivo: true,
            emailDestinatario: "usuario@example.com",
            emailVencimento: true,
            emailDiasAntecedencia: 5,
            emailLimiteCategoria: true,
            pushAtivo: true,
            pushVencimento: false,
            pushDiasAntecedencia: 2,
            pushLimiteCategoria: true);

        config.EmailAtivo.Should().BeTrue();
        config.EmailDestinatario.Should().Be("usuario@example.com");
        config.EmailVencimento.Should().BeTrue();
        config.EmailDiasAntecedencia.Should().Be(5);
        config.EmailLimiteCategoria.Should().BeTrue();
        config.PushAtivo.Should().BeTrue();
        config.PushVencimento.Should().BeFalse();
        config.PushDiasAntecedencia.Should().Be(2);
        config.PushLimiteCategoria.Should().BeTrue();
    }

    [Fact]
    public void Atualizar_EmailDestinatarioBranco_DeveArmazenarNull()
    {
        var config = ConfigPadrao();
        config.Atualizar(true, "   ", true, 3, false, false, true, 1, false);
        config.EmailDestinatario.Should().BeNull();
    }

    [Fact]
    public void Atualizar_DiasAntecedenciaAbaixoDoMinimo_DeveClampear()
    {
        var config = ConfigPadrao();
        config.Atualizar(false, null, true, 0, false, false, true, -5, false);
        config.EmailDiasAntecedencia.Should().Be(1);
        config.PushDiasAntecedencia.Should().Be(1);
    }

    [Fact]
    public void Atualizar_DiasAntecedenciaAcimaDoMaximo_DeveClampear()
    {
        var config = ConfigPadrao();
        config.Atualizar(false, null, true, 100, false, false, true, 999, false);
        config.EmailDiasAntecedencia.Should().Be(30);
        config.PushDiasAntecedencia.Should().Be(30);
    }

    [Fact]
    public void Atualizar_EmailDestinatario_DeveTrimmar()
    {
        var config = ConfigPadrao();
        config.Atualizar(true, "  email@test.com  ", true, 3, false, false, true, 1, false);
        config.EmailDestinatario.Should().Be("email@test.com");
    }
}
