namespace ControleFinanceiro.SharedKernel.Common;

public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
}
