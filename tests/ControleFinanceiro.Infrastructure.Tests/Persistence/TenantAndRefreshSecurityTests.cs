using ControleFinanceiro.Domain.Cadastros.Pessoas;
using ControleFinanceiro.Domain.Identidade;
using ControleFinanceiro.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Infrastructure.Tests.Persistence;

public sealed class TenantAndRefreshSecurityTests
{
    [Fact]
    public async Task ConsultasSemWorkspace_NaoDevemExporNenhumTenant()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=False");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var familiaA = Guid.NewGuid();
        var familiaB = Guid.NewGuid();
        await using (var seed = new AppDbContext(options))
        {
            await seed.Database.EnsureCreatedAsync();
            foreach (var familia in new[] { familiaA, familiaB, Guid.Empty })
            {
                var pessoa = Pessoa.Criar("Pessoa", TipoPessoa.Fisica, null, null, null, null, [], true);
                if (familia != Guid.Empty) pessoa.AtribuirFamilia(familia);
                seed.Pessoas.Add(pessoa);
            }
            await seed.SaveChangesAsync();
        }
        await using var db = new AppDbContext(options);
        (await db.Pessoas.CountAsync()).Should().Be(0);
        db.DefinirWorkspaceCorrente(familiaA);
        (await db.Pessoas.Select(p => p.FamiliaId).ToArrayAsync()).Should().Equal(familiaA);
        db.DefinirWorkspaceCorrente(familiaB);
        (await db.Pessoas.Select(p => p.FamiliaId).ToArrayAsync()).Should().Equal(familiaB);
    }

    [Fact]
    public async Task RefreshConcorrente_DeveRevogarUmaVezERollbackDoTokenPerdedor()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=False");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var usuarioId = Guid.NewGuid();
        var agora = DateTime.UtcNow;
        await using (var seed = new AppDbContext(options))
        {
            await seed.Database.EnsureCreatedAsync();
            seed.RefreshTokens.Add(RefreshToken.Criar(usuarioId, "original", agora.AddDays(1)));
            await seed.SaveChangesAsync();
        }
        await using var a = new AppDbContext(options);
        await using var b = new AppDbContext(options);
        var tokenA = await a.RefreshTokens.SingleAsync();
        var tokenB = await b.RefreshTokens.SingleAsync();
        tokenA.Revogar(agora, "vencedor");
        tokenB.Revogar(agora, "perdedor");
        a.RefreshTokens.Add(RefreshToken.Criar(usuarioId, "vencedor", agora.AddDays(1)));
        b.RefreshTokens.Add(RefreshToken.Criar(usuarioId, "perdedor", agora.AddDays(1)));
        await a.SaveChangesAsync();
        var salvar = async () => await b.SaveChangesAsync();
        await salvar.Should().ThrowAsync<DbUpdateConcurrencyException>();
        await using var verificar = new AppDbContext(options);
        (await verificar.RefreshTokens.Select(t => t.TokenHash).ToListAsync())
            .Should().BeEquivalentTo("original", "vencedor");
    }
}
