using ControleFinanceiro.Domain.Events;
using ControleFinanceiro.Domain.Financeiro.Events;
using Microsoft.Extensions.Logging;

namespace ControleFinanceiro.Infrastructure.Events.Handlers;

public sealed class ContaPagarCriadaEventHandler(ILogger<ContaPagarCriadaEventHandler> logger)
    : IDomainEventHandler<ContaPagarCriadaEvent>
{
    public Task HandleAsync(ContaPagarCriadaEvent domainEvent, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "ContaPagar criada: {ContaPagarId} — {Descricao} — Valor: {ValorLiquido:N2} — Vencimento: {DataVencimento} — Recebedor: {RecebedorId} — Parcela: {NumeroParcela}/{QuantidadeParcelas}",
            domainEvent.ContaPagarId,
            domainEvent.Descricao,
            domainEvent.ValorLiquido,
            domainEvent.DataVencimento,
            domainEvent.RecebedorId,
            domainEvent.NumeroParcela,
            domainEvent.QuantidadeParcelas);

        return Task.CompletedTask;
    }
}
