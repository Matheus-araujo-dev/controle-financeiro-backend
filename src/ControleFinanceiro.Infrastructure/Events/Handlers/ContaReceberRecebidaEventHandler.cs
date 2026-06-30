using ControleFinanceiro.Domain.Events;
using ControleFinanceiro.Domain.Financeiro.Events;
using Microsoft.Extensions.Logging;

namespace ControleFinanceiro.Infrastructure.Events.Handlers;

public sealed class ContaReceberRecebidaEventHandler(ILogger<ContaReceberRecebidaEventHandler> logger)
    : IDomainEventHandler<ContaReceberRecebidaEvent>
{
    public Task HandleAsync(ContaReceberRecebidaEvent domainEvent, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "ContaReceber recebida: {ContaReceberId} — {Descricao} — Valor: {ValorLiquido:N2} — Data: {DataLiquidacao} — Pagador: {PagadorId} — ContaBancaria: {ContaBancariaId}",
            domainEvent.ContaReceberId,
            domainEvent.Descricao,
            domainEvent.ValorLiquido,
            domainEvent.DataLiquidacao,
            domainEvent.PagadorId,
            domainEvent.ContaBancariaId);

        return Task.CompletedTask;
    }
}
