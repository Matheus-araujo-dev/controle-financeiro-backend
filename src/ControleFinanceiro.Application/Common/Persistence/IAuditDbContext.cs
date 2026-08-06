namespace ControleFinanceiro.Application.Common.Persistence;

public sealed record AuditEntryDto(
    Guid Id,
    string EntityName,
    Guid EntityId,
    string Action,
    string ExecutedBy,
    DateTime OccurredAtUtc,
    string? BeforeJson,
    string? AfterJson);

public interface IAuditDbContext
{
    Task<IReadOnlyList<AuditEntryDto>> GetAuditEntriesAsync(
        string entityName,
        Guid entityId,
        CancellationToken cancellationToken);
}
