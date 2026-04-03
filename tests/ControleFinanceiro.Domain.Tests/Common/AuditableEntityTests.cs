using ControleFinanceiro.SharedKernel.Common;
using FluentAssertions;

namespace ControleFinanceiro.Domain.Tests.Common;

public sealed class AuditableEntityTests
{
    [Fact]
    public void StampCreation_ShouldFillCreationAndUpdateMetadata()
    {
        var entity = new TestAuditableEntity();
        var utcNow = new DateTime(2026, 4, 3, 12, 0, 0, DateTimeKind.Utc);

        entity.StampCreation(utcNow, "bootstrap-user");

        entity.CreatedAtUtc.Should().Be(utcNow);
        entity.UpdatedAtUtc.Should().Be(utcNow);
        entity.CreatedBy.Should().Be("bootstrap-user");
        entity.UpdatedBy.Should().Be("bootstrap-user");
    }

    [Fact]
    public void StampUpdate_ShouldDefaultUnknownUserToSystem()
    {
        var entity = new TestAuditableEntity();
        var createdAt = new DateTime(2026, 4, 3, 12, 0, 0, DateTimeKind.Utc);
        var updatedAt = createdAt.AddMinutes(5);

        entity.StampCreation(createdAt, "bootstrap-user");
        entity.StampUpdate(updatedAt, " ");

        entity.CreatedAtUtc.Should().Be(createdAt);
        entity.UpdatedAtUtc.Should().Be(updatedAt);
        entity.UpdatedBy.Should().Be("system");
    }

    private sealed class TestAuditableEntity : AuditableEntity;
}
