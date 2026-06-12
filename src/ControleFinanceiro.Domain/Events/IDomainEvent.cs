namespace ControleFinanceiro.Domain.Events;

public interface IDomainEvent
{
    Guid Id { get; }

    DateTimeOffset OccurredAtUtc { get; }
}