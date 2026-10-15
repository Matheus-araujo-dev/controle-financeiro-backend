using ControleFinanceiro.Domain.Identidade;
using FluentAssertions;

namespace ControleFinanceiro.Domain.Tests.Identidade;

public sealed class ApiKeyTests
{
    private static readonly DateTime UtcNow = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid FamiliaId = Guid.NewGuid();

    private static ApiKey CriarApiKey(DateTime? expiraEmUtc = null) =>
        ApiKey.Criar("Agente WhatsApp", "hash-abc", "cfk_abcd", FamiliaId, expiraEmUtc: expiraEmUtc);

    [Fact]
    public void Criar_DevePreencherCamposEAtivar()
    {
        var key = CriarApiKey();

        key.Nome.Should().Be("Agente WhatsApp");
        key.KeyHash.Should().Be("hash-abc");
        key.KeyPrefix.Should().Be("cfk_abcd");
        key.FamiliaId.Should().Be(FamiliaId);
        key.Papel.Should().Be("Agente");
        key.Ativo.Should().BeTrue();
        key.ExpiraEmUtc.Should().BeNull();
        key.UltimoUsoEmUtc.Should().BeNull();
    }

    [Fact]
    public void Criar_ComPapelCustomizado_DeveAtribuir()
    {
        var key = ApiKey.Criar("Bot", "hash", "pre_", FamiliaId, papel: "Admin");
        key.Papel.Should().Be("Admin");
    }

    [Fact]
    public void Criar_ComPapelVazio_DeveUsarPadrao()
    {
        var key = ApiKey.Criar("Bot", "hash", "pre_", FamiliaId, papel: " ");
        key.Papel.Should().Be("Agente");
    }

    [Fact]
    public void Criar_SemNome_DeveFalhar()
    {
        var act = () => ApiKey.Criar(" ", "hash", "pre_", FamiliaId);
        act.Should().Throw<ArgumentException>().WithParameterName("nome");
    }

    [Fact]
    public void Criar_SemKeyHash_DeveFalhar()
    {
        var act = () => ApiKey.Criar("Bot", " ", "pre_", FamiliaId);
        act.Should().Throw<ArgumentException>().WithParameterName("keyHash");
    }

    [Fact]
    public void Criar_SemKeyPrefix_DeveFalhar()
    {
        var act = () => ApiKey.Criar("Bot", "hash", " ", FamiliaId);
        act.Should().Throw<ArgumentException>().WithParameterName("keyPrefix");
    }

    [Fact]
    public void Criar_SemFamilia_DeveFalhar()
    {
        var act = () => ApiKey.Criar("Bot", "hash", "pre_", Guid.Empty);
        act.Should().Throw<ArgumentException>().WithParameterName("familiaId");
    }

    [Fact]
    public void Criar_DeveNormalizarNome()
    {
        var key = ApiKey.Criar("  Bot Prod  ", "hash", "pre_", FamiliaId);
        key.Nome.Should().Be("Bot Prod");
    }

    [Fact]
    public void EstaValida_AtivaESemExpiracao_DeveSerVerdadeiro()
    {
        var key = CriarApiKey();
        key.EstaValida(UtcNow).Should().BeTrue();
    }

    [Fact]
    public void EstaValida_AtivaEComExpiracaoFutura_DeveSerVerdadeiro()
    {
        var key = CriarApiKey(UtcNow.AddDays(30));
        key.EstaValida(UtcNow).Should().BeTrue();
    }

    [Fact]
    public void EstaValida_AtivaEExpirada_DeveSerFalso()
    {
        var key = CriarApiKey(UtcNow.AddHours(-1));
        key.EstaValida(UtcNow).Should().BeFalse();
    }

    [Fact]
    public void EstaValida_Revogada_DeveSerFalso()
    {
        var key = CriarApiKey();
        key.Revogar();
        key.EstaValida(UtcNow).Should().BeFalse();
    }

    [Fact]
    public void Revogar_DeveInativar()
    {
        var key = CriarApiKey();
        key.Revogar();
        key.Ativo.Should().BeFalse();
    }

    [Fact]
    public void RegistrarUso_DeveAtualizarTimestamp()
    {
        var key = CriarApiKey();
        key.RegistrarUso(UtcNow);
        key.UltimoUsoEmUtc.Should().Be(UtcNow);
    }

    [Fact]
    public void EstaValida_ExpiracaoExata_DeveSerFalso()
    {
        var key = CriarApiKey(UtcNow);
        key.EstaValida(UtcNow).Should().BeFalse();
    }
}
