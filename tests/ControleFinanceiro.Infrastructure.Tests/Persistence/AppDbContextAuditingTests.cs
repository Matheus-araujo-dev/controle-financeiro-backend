using ControleFinanceiro.Domain.Cadastros.Pessoas;
using ControleFinanceiro.Infrastructure.Persistence;
using ControleFinanceiro.SharedKernel.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Infrastructure.Tests.Persistence;

public sealed class AppDbContextAuditingTests
{
    [Fact]
    public async Task SaveChangesAsync_DeveCarimbarAuditoriaECriarTrail()
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var clock = new FakeClock(new DateTime(2026, 4, 3, 20, 30, 0, DateTimeKind.Utc));
        var currentUser = new FakeCurrentUser("tester");

        await using var context = new AppDbContext(options, clock, currentUser);
        await context.Database.EnsureCreatedAsync();

        var pessoa = Pessoa.Criar(
            "Cliente Exemplo",
            TipoPessoa.Fisica,
            "123.456.789-01",
            "cliente@example.com",
            "5531999998888",
            "Observacao sensivel",
            [],
            true);
        context.Pessoas.Add(pessoa);

        await context.SaveChangesAsync();

        pessoa.CreatedAtUtc.Should().Be(clock.UtcNow);
        pessoa.UpdatedAtUtc.Should().Be(clock.UtcNow);
        pessoa.CreatedBy.Should().Be("tester");
        pessoa.UpdatedBy.Should().Be("tester");

        var auditTrail = await context.AuditTrailEntries.SingleAsync();
        auditTrail.EntityName.Should().Be(nameof(Pessoa));
        auditTrail.Action.Should().Be("Created");
        auditTrail.ExecutedBy.Should().Be("tester");
        auditTrail.AfterJson.Should().Contain("[REDACTED]");
        auditTrail.AfterJson.Should().NotContain("Cliente Exemplo");
        auditTrail.AfterJson.Should().NotContain("12345678901");
        auditTrail.AfterJson.Should().NotContain("cliente@example.com");
        auditTrail.AfterJson.Should().NotContain("5531999998888");
    }

    private sealed class FakeClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class FakeCurrentUser(string userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public string? UserId { get; } = userId;

        public Guid? WorkspaceId => null;

        public Guid? FamiliaId => WorkspaceId;

        public string? Papel => null;
    }
}


