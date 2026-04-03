using ControleFinanceiro.SharedKernel.Abstractions;

namespace ControleFinanceiro.Infrastructure.Identity;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
