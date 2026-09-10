using ControleFinanceiro.Application.Cadastros.ContasGerenciais;
using ControleFinanceiro.Application.Common.Exceptions;
using ControleFinanceiro.Application.Identidade;
using ControleFinanceiro.Domain.Identidade;
using ControleFinanceiro.Infrastructure.Persistence;
using ControleFinanceiro.SharedKernel.Abstractions;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace ControleFinanceiro.Infrastructure.Tests.Persistence;

public sealed class AuthRefreshConcurrencyTests
{
    [Fact]
    public async Task Refresh_QuandoPerderConcorrencia_DeveFalharAutenticacaoELimparSucessorNaoPersistido()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var interceptor = new ConflictOnSave();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).AddInterceptors(interceptor).Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var usuario = Usuario.Criar("subject", "usuario@example.com", "Usuario", null);
        var familia = Familia.Criar("Workspace");
        usuario.DefinirFamiliaAtiva(familia.Id);
        db.Usuarios.Add(usuario);
        db.Familias.Add(familia);
        db.MembrosFamilia.Add(MembroFamilia.Criar(familia.Id, usuario.Id, PapelFamilia.Administrador));
        db.RefreshTokens.Add(RefreshToken.Criar(usuario.Id, "original", DateTime.UtcNow.AddDays(1)));
        await db.SaveChangesAsync();
        interceptor.Fail = true;
        var service = new AuthAppService(db, new UnusedGoogleValidator(), new FakeTokens(), new Clock(),
            Options.Create(new IdentidadeOptions()), new ContasGerenciaisPadraoSeedService(db));
        var refresh = () => service.RefreshAsync("original", CancellationToken.None);
        await refresh.Should().ThrowAsync<AuthenticationFailedException>();
        db.ChangeTracker.Entries().Should().BeEmpty();
        (await db.RefreshTokens.CountAsync()).Should().Be(1);
        (await db.RefreshTokens.SingleAsync()).RevogadoEmUtc.Should().BeNull();
    }

    private sealed class ConflictOnSave : SaveChangesInterceptor
    {
        public bool Fail { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Fail) throw new DbUpdateConcurrencyException("Outra renovacao venceu");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
    private sealed class Clock : IClock { public DateTime UtcNow => DateTime.UtcNow; }
    private sealed class UnusedGoogleValidator : IGoogleTokenValidator
    {
        public Task<GoogleUserInfo> ValidateAsync(string idToken, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
    private sealed class FakeTokens : ITokenService
    {
        public AccessTokenResult CreateAccessToken(Usuario usuario, Guid familiaId, PapelFamilia papel) => new("access", DateTime.UtcNow.AddMinutes(30));
        public string GenerateOpaqueToken() => "sucessor";
        public string HashToken(string token) => token;
        public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(7);
    }
}
