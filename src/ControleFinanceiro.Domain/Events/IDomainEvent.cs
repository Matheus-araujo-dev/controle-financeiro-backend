using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.Events;

// Alias para SharedKernel.IDomainEvent — mantém compatibilidade com usages existentes.
public interface IDomainEvent : SharedKernel.Common.IDomainEvent
{
}