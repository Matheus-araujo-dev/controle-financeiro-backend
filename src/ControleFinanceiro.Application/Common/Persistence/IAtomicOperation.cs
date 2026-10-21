namespace ControleFinanceiro.Application.Common.Persistence;

public interface IAtomicOperation
{
    Task<T> ExecutarAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken);
}
