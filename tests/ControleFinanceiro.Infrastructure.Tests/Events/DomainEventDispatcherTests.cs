using ControleFinanceiro.Domain.Events;
using ControleFinanceiro.Infrastructure.Events;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ControleFinanceiro.Infrastructure.Tests.Events;

public sealed class DomainEventDispatcherTests
{
    public sealed record EventoTeste(string Valor) : IDomainEvent
    {
        public Guid Id { get; } = Guid.NewGuid();
        public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
    }

    public sealed class HandlerTeste : IDomainEventHandler<EventoTeste>
    {
        public List<string> Recebidos { get; } = [];

        public Task HandleAsync(EventoTeste domainEvent, CancellationToken cancellationToken = default)
        {
            Recebidos.Add(domainEvent.Valor);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task DispatchAsync_DeveInvocarHandlerRegistrado()
    {
        var handler = new HandlerTeste();
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventHandler<EventoTeste>>(handler);
        var dispatcher = new DomainEventDispatcher(services.BuildServiceProvider());

        await dispatcher.DispatchAsync(new EventoTeste("a"), CancellationToken.None);

        handler.Recebidos.Should().ContainSingle().Which.Should().Be("a");
    }

    [Fact]
    public async Task DispatchAsync_ComVariosEventos_DeveInvocarParaCadaUm()
    {
        var handler = new HandlerTeste();
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventHandler<EventoTeste>>(handler);
        var dispatcher = new DomainEventDispatcher(services.BuildServiceProvider());

        await dispatcher.DispatchAsync(
            [new EventoTeste("x"), new EventoTeste("y")],
            CancellationToken.None);

        handler.Recebidos.Should().BeEquivalentTo("x", "y");
    }

    [Fact]
    public async Task DispatchAsync_SemHandler_NaoDeveLancar()
    {
        var dispatcher = new DomainEventDispatcher(new ServiceCollection().BuildServiceProvider());

        var acao = async () => await dispatcher.DispatchAsync(new EventoTeste("z"), CancellationToken.None);

        await acao.Should().NotThrowAsync();
    }
}
