using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ControleFinanceiro.Domain.Identidade;
using ControleFinanceiro.Infrastructure.Identity;
using ControleFinanceiro.SharedKernel.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace ControleFinanceiro.Infrastructure.Tests.Identity;

public sealed class JwtTokenServiceTests
{
    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private static JwtTokenService CriarServico(JwtOptions? options = null, DateTime? agora = null)
    {
        options ??= new JwtOptions
        {
            JwtSigningKey = "uma-chave-de-assinatura-bem-grande-com-mais-de-32-bytes!!",
            JwtIssuer = "emissor-teste",
            JwtAudience = "audiencia-teste",
            AccessTokenMinutes = 30,
            RefreshTokenDays = 15
        };

        return new JwtTokenService(Options.Create(options), new FixedClock(agora ?? DateTime.UtcNow));
    }

    private static Usuario CriarUsuario() =>
        Usuario.Criar("google-sub-123", "usuario@teste.local", "Usuario Teste", null);

    [Fact]
    public void CreateAccessToken_DeveEmitirTokenComClaimsEsperadas()
    {
        var agora = new DateTime(2026, 6, 20, 12, 0, 0, DateTimeKind.Utc);
        var servico = CriarServico(agora: agora);
        var usuario = CriarUsuario();
        var familiaId = Guid.NewGuid();

        var resultado = servico.CreateAccessToken(usuario, familiaId, PapelFamilia.Administrador);

        resultado.ExpiresAtUtc.Should().Be(agora.AddMinutes(30));

        var token = new JwtSecurityTokenHandler().ReadJwtToken(resultado.AccessToken);
        token.Issuer.Should().Be("emissor-teste");
        token.Audiences.Should().Contain("audiencia-teste");
        token.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == usuario.Id.ToString());
        token.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "usuario@teste.local");
        token.Claims.Should().Contain(c => c.Type == JwtTokenService.WorkspaceClaim && c.Value == familiaId.ToString());
        token.Claims.Should().Contain(c => c.Type == JwtTokenService.FamiliaClaim && c.Value == familiaId.ToString());
        token.Claims.Should().Contain(c => c.Type == JwtTokenService.PapelClaim && c.Value == nameof(PapelFamilia.Administrador));
        token.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Jti);
    }

    [Fact]
    public void CreateAccessToken_DeveGerarJtiUnicoPorToken()
    {
        var servico = CriarServico();
        var usuario = CriarUsuario();
        var familiaId = Guid.NewGuid();

        var handler = new JwtSecurityTokenHandler();
        var jti1 = handler.ReadJwtToken(servico.CreateAccessToken(usuario, familiaId, PapelFamilia.Membro).AccessToken)
            .Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        var jti2 = handler.ReadJwtToken(servico.CreateAccessToken(usuario, familiaId, PapelFamilia.Membro).AccessToken)
            .Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

        jti1.Should().NotBe(jti2);
    }

    [Fact]
    public void CreateAccessToken_SemChaveDeAssinatura_DeveLancar()
    {
        var servico = CriarServico(new JwtOptions { JwtSigningKey = "" });

        var acao = () => servico.CreateAccessToken(CriarUsuario(), Guid.NewGuid(), PapelFamilia.Membro);

        acao.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RefreshTokenLifetime_DeveRefletirOpcaoConfigurada()
    {
        var servico = CriarServico(new JwtOptions
        {
            JwtSigningKey = "uma-chave-de-assinatura-bem-grande-com-mais-de-32-bytes!!",
            RefreshTokenDays = 21
        });

        servico.RefreshTokenLifetime.Should().Be(TimeSpan.FromDays(21));
    }

    [Fact]
    public void GenerateOpaqueToken_DeveSerUrlSafeUnicoESemPadding()
    {
        var servico = CriarServico();

        var t1 = servico.GenerateOpaqueToken();
        var t2 = servico.GenerateOpaqueToken();

        t1.Should().NotBe(t2);
        t1.Should().NotContain("+").And.NotContain("/").And.NotEndWith("=");
        t1.Length.Should().BeGreaterThan(40);
    }

    [Fact]
    public void HashToken_DeveSerDeterministicoEHexadecimal()
    {
        var servico = CriarServico();

        var hash1 = servico.HashToken("token-abc");
        var hash2 = servico.HashToken("token-abc");
        var hashDiferente = servico.HashToken("token-xyz");

        hash1.Should().Be(hash2);
        hash1.Should().NotBe(hashDiferente);
        hash1.Should().MatchRegex("^[0-9A-F]{64}$");
    }
}
