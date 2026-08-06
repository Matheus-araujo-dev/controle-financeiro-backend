using ControleFinanceiro.Api.Tests.Infrastructure;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Privacidade;
using ControleFinanceiro.Domain.FinanceAI;
using ControleFinanceiro.Domain.Identidade;
using ControleFinanceiro.SharedKernel.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControleFinanceiro.Api.Tests.Privacidade;

public sealed class PrivacidadeAppServiceTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    // ─── Fakes ────────────────────────────────────────────────────────────────

    private sealed class FakeCurrentUser(string? userId) : ICurrentUser
    {
        public bool IsAuthenticated => userId is not null;
        public string? UserId => userId;
        public string? UserEmail => null;
        public Guid? WorkspaceId => null;
        public Guid? FamiliaId => null;
        public string? Papel => "Administrador";
    }

    private sealed class FakeClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow => utcNow;
    }

    // ─── Helper ───────────────────────────────────────────────────────────────

    private PrivacidadeAppService CriarServico(IAppDbContext db, string? userId = null) =>
        new(db, new FakeCurrentUser(userId), new FakeClock(DateTime.UtcNow));

    // ─── Testes ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task SolicitarEsquecimentoAsync_UsuarioNaoAutenticado_LancaUnauthorized()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        var servico = CriarServico(db, userId: null);

        var act = async () => await servico.SolicitarEsquecimentoAsync(CancellationToken.None);
        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("*autenticado*");
    }

    [Fact]
    public async Task SolicitarEsquecimentoAsync_UserIdInvalido_LancaUnauthorized()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        var servico = CriarServico(db, userId: "nao-e-um-guid");

        var act = async () => await servico.SolicitarEsquecimentoAsync(CancellationToken.None);
        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("*inválido*");
    }

    [Fact]
    public async Task SolicitarEsquecimentoAsync_UsuarioNaoEncontrado_LancaInvalidOperation()
    {
        await _factory.ResetDatabaseAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        var servico = CriarServico(db, userId: Guid.NewGuid().ToString());

        var act = async () => await servico.SolicitarEsquecimentoAsync(CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*não encontrado*");
    }

    [Fact]
    public async Task SolicitarEsquecimentoAsync_UsuarioValido_AnomimizaEmailENome()
    {
        await _factory.ResetDatabaseAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        var usuario = Usuario.Criar("sub-lgpd-test", "real-email@example.com", "Nome Real", null);
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync(CancellationToken.None);

        var servico = CriarServico(db, userId: usuario.Id.ToString());
        await servico.SolicitarEsquecimentoAsync(CancellationToken.None);

        var usuarioAtualizado = await db.Usuarios.AsNoTracking()
            .SingleAsync(u => u.Id == usuario.Id, CancellationToken.None);

        usuarioAtualizado.Email.Should().Contain("anonimizado.local");
        usuarioAtualizado.Email.Should().NotContain("real-email");
        usuarioAtualizado.Nome.Should().Be("Usuário Removido");
        usuarioAtualizado.Ativo.Should().BeFalse();
    }

    [Fact]
    public async Task SolicitarEsquecimentoAsync_ComRefreshTokensAtivos_RevogaTodos()
    {
        await _factory.ResetDatabaseAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        var usuario = Usuario.Criar("sub-tokens-test", "tokens@example.com", "Com Tokens", null);
        db.Usuarios.Add(usuario);

        var expira = DateTime.UtcNow.AddDays(30);
        var token1 = RefreshToken.Criar(usuario.Id, "hash-1", expira);
        var token2 = RefreshToken.Criar(usuario.Id, "hash-2", expira);
        db.RefreshTokens.AddRange(token1, token2);

        await db.SaveChangesAsync(CancellationToken.None);

        var servico = CriarServico(db, userId: usuario.Id.ToString());
        await servico.SolicitarEsquecimentoAsync(CancellationToken.None);

        var tokens = await db.RefreshTokens.AsNoTracking()
            .Where(t => t.UsuarioId == usuario.Id)
            .ToListAsync(CancellationToken.None);

        tokens.Should().HaveCount(2);
        tokens.All(t => t.RevogadoEmUtc.HasValue).Should().BeTrue();
    }

    [Fact]
    public async Task SolicitarEsquecimentoAsync_NaoAfetaOutrosUsuarios()
    {
        await _factory.ResetDatabaseAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        var usuarioAlvo = Usuario.Criar("sub-alvo", "alvo@example.com", "Usuário Alvo", null);
        var usuarioOutro = Usuario.Criar("sub-outro", "outro@example.com", "Usuário Outro", null);
        db.Usuarios.AddRange(usuarioAlvo, usuarioOutro);
        await db.SaveChangesAsync(CancellationToken.None);

        var servico = CriarServico(db, userId: usuarioAlvo.Id.ToString());
        await servico.SolicitarEsquecimentoAsync(CancellationToken.None);

        var outro = await db.Usuarios.AsNoTracking()
            .SingleAsync(u => u.Id == usuarioOutro.Id, CancellationToken.None);

        outro.Email.Should().Be("outro@example.com");
        outro.Nome.Should().Be("Usuário Outro");
        outro.Ativo.Should().BeTrue();
    }

    [Fact]
    public async Task SolicitarEsquecimentoAsync_ComWhatsappVinculado_RemoveWhatsapp()
    {
        await _factory.ResetDatabaseAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        var familia = await CriarFamiliaMinimAsync(db);
        var usuario = Usuario.Criar("sub-wpp-test", "wpp@example.com", "Com WhatsApp", null);
        usuario.DefinirFamiliaAtiva(familia);
        db.Usuarios.Add(usuario);

        var wpp = WhatsappUsuario.Criar(familia, usuario.Id, "5531988880000");
        db.WhatsappUsuarios.Add(wpp);

        await db.SaveChangesAsync(CancellationToken.None);

        var servico = CriarServico(db, userId: usuario.Id.ToString());
        await servico.SolicitarEsquecimentoAsync(CancellationToken.None);

        var wppRestante = await db.WhatsappUsuarios.AsNoTracking()
            .Where(w => w.UsuarioId == usuario.Id)
            .ToListAsync(CancellationToken.None);

        wppRestante.Should().BeEmpty();
    }

    // ─── Helper de seed mínimo ────────────────────────────────────────────────

    private static async Task<Guid> CriarFamiliaMinimAsync(IAppDbContext db)
    {
        // Familia sem membros — precisamos apenas do ID para associar WhatsApp
        var familia = Domain.Identidade.Familia.Criar("Família Teste");
        db.Familias.Add(familia);
        await db.SaveChangesAsync(CancellationToken.None);
        return familia.Id;
    }
}
