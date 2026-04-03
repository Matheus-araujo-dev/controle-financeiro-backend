namespace ControleFinanceiro.SharedKernel.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }
}
