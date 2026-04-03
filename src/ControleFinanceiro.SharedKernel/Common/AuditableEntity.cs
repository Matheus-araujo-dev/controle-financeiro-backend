namespace ControleFinanceiro.SharedKernel.Common;

public abstract class AuditableEntity : Entity
{
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    public void StampCreation(DateTime utcNow, string? userId)
    {
        var normalizedUserId = NormalizeUserId(userId);

        CreatedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
        CreatedBy = normalizedUserId;
        UpdatedBy = normalizedUserId;
    }

    public void StampUpdate(DateTime utcNow, string? userId)
    {
        if (CreatedAtUtc == default)
        {
            StampCreation(utcNow, userId);
            return;
        }

        UpdatedAtUtc = utcNow;
        UpdatedBy = NormalizeUserId(userId);
    }

    private static string NormalizeUserId(string? userId) =>
        string.IsNullOrWhiteSpace(userId) ? "system" : userId.Trim();
}
