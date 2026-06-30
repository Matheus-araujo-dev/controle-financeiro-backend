using ControleFinanceiro.Domain.Events;
using ControleFinanceiro.Domain.Financeiro.Events;
using Microsoft.Extensions.Logging;

namespace ControleFinanceiro.Infrastructure.Events.Handlers;

public sealed class ContaPagarLiquidadaEventHandler(ILogger<ContaPagarLiquidadaEventHandler> logger)
    : IDomainEventHandler<ContaPagarLiquidadaEvent>
{
    public Task HandleAsync(ContaPagarLiquidadaEvent domainEvent, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "ContaPagar liquidada: {ContaPagarId} — {Descricao} — Valor: {ValorLiquido:N2} — Data: {DataLiquidacao} — ContaBancaria: {ContaBancariaId}",
            domainEvent.ContaPagarId,
            domainEvent.Descricao,
            domainEvent.ValorLiquido,
            domainEvent.DataLiquidacao,
            domainEvent.ContaBancariaId);

        return Task.CompletedTask;
    }
}
