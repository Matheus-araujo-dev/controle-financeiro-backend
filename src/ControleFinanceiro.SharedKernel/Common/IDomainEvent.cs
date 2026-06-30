namespace ControleFinanceiro.SharedKernel.Common;

public interface IDomainEvent
{
    Guid Id { get; }

    DateTimeOffset OccurredAtUtc { get; }
}
