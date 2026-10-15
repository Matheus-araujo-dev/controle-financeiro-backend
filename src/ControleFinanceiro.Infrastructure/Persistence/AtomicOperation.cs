using System.Data;
using ControleFinanceiro.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Infrastructure.Persistence;

public sealed class AtomicOperation(AppDbContext db) : IAtomicOperation
{
    public async Task<T> ExecutarAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is not null) return await operation();
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                var result = await operation();
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
                throw;
            }
        });
    }
}
