using ControleFinanceiro.Domain.FinanceAI;
using FluentAssertions;

namespace ControleFinanceiro.Application.Tests.FinanceAI;

public sealed class WhatsappUsuarioDomainTests
{
    private static readonly Guid FamiliaId = Guid.NewGuid();
    private static readonly Guid UsuarioId = Guid.NewGuid();

    [Theory]
    [InlineData("+55 31 99999-8888", "5531999998888")]
    [InlineData("55 (31) 9 9999-8888", "5531999998888")]
    [InlineData("5531999998888", "5531999998888")]
    [InlineData("31999998888", "31999998888")]
    public void NormalizarTelefone_DeveRemoverCaracteresNaoNumericos(string entrada, string esperado)
    {
        WhatsappUsuario.NormalizarTelefone(entrada).Should().Be(esperado);
    }

    [Fact]
    public void Criar_DeveIniciarInativo()
    {
        var wup = WhatsappUsuario.Criar(FamiliaId, UsuarioId, "5531999998888");

        wup.Ativo.Should().BeFalse();
        wup.VerificadoEm.Should().BeNull();
        wup.Telefone.Should().Be("5531999998888");
        wup.FamiliaId.Should().Be(FamiliaId);
        wup.UsuarioId.Should().Be(UsuarioId);
    }

    [Fact]
    public void Verificar_DeveAtivarERegistrarTimestamp()
    {
        var wup = WhatsappUsuario.Criar(FamiliaId, UsuarioId, "5531999998888");
        var agora = DateTimeOffset.UtcNow;

        wup.Verificar(agora);

        wup.Ativo.Should().BeTrue();
        wup.VerificadoEm.Should().Be(agora);
    }

    [Fact]
    public void AtualizarTelefone_DeveNormalizarEAtivar()
    {
        var wup = WhatsappUsuario.Criar(FamiliaId, UsuarioId, "5531000000000");
        var novoTelefone = "+55 31 88888-7777";
        var agora = DateTimeOffset.UtcNow;

        wup.AtualizarTelefone(novoTelefone, agora);

        wup.Telefone.Should().Be("5531888887777");
        wup.Ativo.Should().BeTrue();
        wup.VerificadoEm.Should().Be(agora);
    }

    [Fact]
    public void Desativar_DeveSetarAtivoFalso()
    {
        var wup = WhatsappUsuario.Criar(FamiliaId, UsuarioId, "5531999998888");
        wup.Verificar(DateTimeOffset.UtcNow);

        wup.Desativar();

        wup.Ativo.Should().BeFalse();
    }
}
