using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.Events;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(SharedKernel.Common.IDomainEvent domainEvent, CancellationToken cancellationToken = default);

    Task DispatchAsync(IEnumerable<SharedKernel.Common.IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}