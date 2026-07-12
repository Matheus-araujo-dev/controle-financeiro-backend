using ControleFinanceiro.Domain.FinanceAI;
using FluentAssertions;
using Xunit;

namespace ControleFinanceiro.Domain.Tests.FinanceAI;

public class PushSubscriptionTests
{
    private static readonly Guid FamiliaId = Guid.NewGuid();
    private static readonly Guid UsuarioId = Guid.NewGuid();

    private static PushSubscription SubscriptionValida() =>
        PushSubscription.Registrar(FamiliaId, UsuarioId, "https://fcm.push.endpoint", "p256dhkey", "authsecret");

    [Fact]
    public void Registrar_ComDadosValidos_DeveCriarAtiva()
    {
        var sub = SubscriptionValida();

        sub.UsuarioId.Should().Be(UsuarioId);
        sub.FamiliaId.Should().Be(FamiliaId);
        sub.Endpoint.Should().Be("https://fcm.push.endpoint");
        sub.P256dh.Should().Be("p256dhkey");
        sub.Auth.Should().Be("authsecret");
        sub.Ativo.Should().BeTrue();
        sub.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Registrar_EndpointVazio_DeveLancarExcecao()
    {
        var acao = () => PushSubscription.Registrar(FamiliaId, UsuarioId, "   ", "key", "auth");
        acao.Should().Throw<ArgumentException>().WithParameterName("endpoint");
    }

    [Fact]
    public void Registrar_P256dhVazio_DeveLancarExcecao()
    {
        var acao = () => PushSubscription.Registrar(FamiliaId, UsuarioId, "https://endpoint", "", "auth");
        acao.Should().Throw<ArgumentException>().WithParameterName("p256dh");
    }

    [Fact]
    public void Registrar_AuthVazio_DeveLancarExcecao()
    {
        var acao = () => PushSubscription.Registrar(FamiliaId, UsuarioId, "https://endpoint", "key", "");
        acao.Should().Throw<ArgumentException>().WithParameterName("auth");
    }

    [Fact]
    public void Registrar_EndpointComEspacos_DeveTrimmar()
    {
        var sub = PushSubscription.Registrar(FamiliaId, UsuarioId, "  https://endpoint  ", "key", "auth");
        sub.Endpoint.Should().Be("https://endpoint");
    }

    [Fact]
    public void Desativar_DeveMarcarComoInativo()
    {
        var sub = SubscriptionValida();
        sub.Ativo.Should().BeTrue();

        sub.Desativar();

        sub.Ativo.Should().BeFalse();
    }
}
