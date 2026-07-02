using System.Security.Claims;
using ControleFinanceiro.Infrastructure.Identity;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace ControleFinanceiro.Infrastructure.Tests.Identity;

public sealed class HttpCurrentUserTests
{
    private static HttpCurrentUser ComUsuario(ClaimsPrincipal? principal, bool comContexto = true)
    {
        var accessor = new HttpContextAccessor();
        if (comContexto)
        {
            accessor.HttpContext = new DefaultHttpContext { User = principal ?? new ClaimsPrincipal(new ClaimsIdentity()) };
        }

        return new HttpCurrentUser(accessor);
    }

    private static ClaimsPrincipal Autenticado(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "Test"));

    [Fact]
    public void UsuarioAutenticado_DeveExporClaims()
    {
        var workspaceId = Guid.NewGuid();
        var sut = ComUsuario(Autenticado(
            new Claim(ClaimTypes.NameIdentifier, "user-1"),
            new Claim(JwtTokenService.WorkspaceClaim, workspaceId.ToString()),
            new Claim(JwtTokenService.PapelClaim, "Administrador")));

        sut.IsAuthenticated.Should().BeTrue();
        sut.UserId.Should().Be("user-1");
        sut.WorkspaceId.Should().Be(workspaceId);
        sut.FamiliaId.Should().Be(workspaceId);
        sut.Papel.Should().Be("Administrador");
    }

    [Fact]
    public void UsuarioComClaimLegada_DeveExporWorkspaceId()
    {
        var familiaId = Guid.NewGuid();
        var sut = ComUsuario(Autenticado(
            new Claim(ClaimTypes.NameIdentifier, "user-2"),
            new Claim(JwtTokenService.FamiliaClaim, familiaId.ToString())));

        sut.WorkspaceId.Should().Be(familiaId);
        sut.FamiliaId.Should().Be(familiaId);
    }

    [Fact]
    public void UsuarioNaoAutenticado_DeveRetornarVazio()
    {
        var sut = ComUsuario(new ClaimsPrincipal(new ClaimsIdentity()));

        sut.IsAuthenticated.Should().BeFalse();
        sut.UserId.Should().BeNull();
        sut.WorkspaceId.Should().BeNull();
        sut.FamiliaId.Should().BeNull();
        sut.Papel.Should().BeNull();
    }

    [Fact]
    public void SemHttpContext_DeveRetornarVazio()
    {
        var sut = ComUsuario(null, comContexto: false);

        sut.IsAuthenticated.Should().BeFalse();
        sut.UserId.Should().BeNull();
        sut.WorkspaceId.Should().BeNull();
        sut.FamiliaId.Should().BeNull();
    }

    [Fact]
    public void WorkspaceClaimInvalida_DeveRetornarNull()
    {
        var sut = ComUsuario(Autenticado(
            new Claim(ClaimTypes.NameIdentifier, "user-1"),
            new Claim(JwtTokenService.WorkspaceClaim, "nao-e-guid")));

        sut.WorkspaceId.Should().BeNull();
        sut.FamiliaId.Should().BeNull();
    }

    [Fact]
    public void UserId_DeveUsarSubQuandoNameIdentifierAusente()
    {
        var sut = ComUsuario(Autenticado(new Claim("sub", "sub-99")));

        sut.UserId.Should().Be("sub-99");
    }
}
