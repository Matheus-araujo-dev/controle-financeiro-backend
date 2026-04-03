using ControleFinanceiro.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Infrastructure.Tests.Persistence;

public sealed class AppDbContextModelTests
{
    [Fact]
    public void Model_ShouldExposeAuditTrailEntryMapping()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=ControleFinanceiroTests;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        using var context = new AppDbContext(options);

        var entityType = context.Model.FindEntityType(typeof(AuditTrailEntry));

        entityType.Should().NotBeNull();
        entityType!.GetTableName().Should().Be("audit_trail_entries");
    }
}
