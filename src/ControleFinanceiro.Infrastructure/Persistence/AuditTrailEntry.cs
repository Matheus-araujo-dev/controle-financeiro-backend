using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Infrastructure.Persistence;

public sealed class AuditTrailEntry : Entity
{
    public string EntityName { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string ExecutedBy { get; private set; } = "system";
    public DateTime OccurredAtUtc { get; private set; }
    public string? BeforeJson { get; private set; }
    public string? AfterJson { get; private set; }

    public static AuditTrailEntry Create(
        string entityName,
        Guid entityId,
        string action,
        DateTime occurredAtUtc,
        string? executedBy = null,
        string? beforeJson = null,
        string? afterJson = null)
    {
        return new AuditTrailEntry
        {
            EntityName = entityName,
            EntityId = entityId,
            Action = action,
            OccurredAtUtc = occurredAtUtc,
            ExecutedBy = string.IsNullOrWhiteSpace(executedBy) ? "system" : executedBy.Trim(),
            BeforeJson = beforeJson,
            AfterJson = afterJson
        };
    }
}
