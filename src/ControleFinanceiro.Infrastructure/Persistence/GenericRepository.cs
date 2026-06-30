using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.SharedKernel.Common;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Infrastructure.Persistence;

/// <summary>
/// EF Core–backed generic repository satisfying IRepository{T} for any Entity.
/// Domain-specific repositories can extend this to add custom queries without
/// duplicating the basic CRUD plumbing.
/// </summary>
public class GenericRepository<T>(AppDbContext dbContext) : IRepository<T> where T : Entity
{
    protected readonly AppDbContext DbContext = dbContext;

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await DbContext.Set<T>().FindAsync([id], cancellationToken);

    public virtual async Task<IReadOnlyList<T>> ListAsync(CancellationToken cancellationToken = default) =>
        await DbContext.Set<T>().AsNoTracking().ToListAsync(cancellationToken);

    public virtual Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        DbContext.Set<T>().Add(entity);
        return Task.CompletedTask;
    }

    public virtual Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        DbContext.Set<T>().Update(entity);
        return Task.CompletedTask;
    }

    public virtual Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        DbContext.Set<T>().Remove(entity);
        return Task.CompletedTask;
    }
}
