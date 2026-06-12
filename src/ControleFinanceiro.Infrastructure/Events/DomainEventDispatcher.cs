using ControleFinanceiro.Domain.Events;
using Microsoft.Extensions.DependencyInjection;

namespace ControleFinanceiro.Infrastructure.Events;

public sealed class DomainEventDispatcher(IServiceProvider serviceProvider) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
        var handlers = serviceProvider.GetServices(handlerType);

        if (handlers != null)
        {
            foreach (var handler in handlers)
            {
                if (handler != null)
                {
                    await ((dynamic)handler).HandleAsync((dynamic)domainEvent, cancellationToken);
                }
            }
        }
    }

    public async Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            await DispatchAsync(domainEvent, cancellationToken);
        }
    }
}
