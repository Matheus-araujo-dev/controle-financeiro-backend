using ControleFinanceiro.SharedKernel.Abstractions;

namespace ControleFinanceiro.Domain.Events;

public abstract class DomainEventBase : IDomainEvent
{
    private static readonly IClock Clock = new DomainEventsClock();

    public Guid Id { get; } = Guid.NewGuid();

    public DateTimeOffset OccurredAtUtc { get; } = new DateTimeOffset(Clock.UtcNow, TimeSpan.Zero);

    private sealed class DomainEventsClock : IClock
    {
        DateTime IClock.UtcNow => DateTime.UtcNow;
    }
}