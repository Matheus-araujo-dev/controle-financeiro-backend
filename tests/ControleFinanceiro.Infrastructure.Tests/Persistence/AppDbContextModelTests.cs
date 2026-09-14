using ControleFinanceiro.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ControleFinanceiro.Infrastructure.Tests.Persistence;

public sealed class AppDbContextModelTests
{
    [Fact]
    public void PostgreSql_ModeloAtual_DeveCorresponderAoSnapshotEMigrationGerarScript()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=test;Username=postgres;Password=postgres")
            .Options;
        using var context = new AppDbContext(options);
        context.Database.HasPendingModelChanges().Should().BeFalse();
        var migrator = context.GetService<IMigrator>();
        var script = migrator.GenerateScript("20260825010515_AddCartaoRecebedorFormaPagamentoPadraoFatura", "20260909000100_RepairLegacyImportedItemWorkspace");
        script.Should().Contain("UPDATE \"itens_importados_whatsapp\"");
    }

    [Fact]
    public void Model_ShouldExposeAuditTrailEntryMapping()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=test;Username=postgres;Password=postgres")
            .Options;

        using var context = new AppDbContext(options);

        var entityType = context.Model.FindEntityType(typeof(AuditTrailEntry));

        entityType.Should().NotBeNull();
        entityType!.GetTableName().Should().Be("audit_trail_entries");
    }
}
