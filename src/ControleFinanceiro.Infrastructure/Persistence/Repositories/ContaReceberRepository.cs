using ControleFinanceiro.Application.Financeiro.ContasReceber;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Infrastructure.Persistence.Repositories;

public sealed class ContaReceberRepository(AppDbContext dbContext)
    : GenericRepository<ContaReceber>(dbContext), IContaReceberRepository
{
    public async Task<ContaReceber?> GetByIdWithRateiosAsync(Guid id, CancellationToken cancellationToken = default) =>
        await DbContext.ContasReceber
            .Include(c => c.Rateios)
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ContaReceber>> ListByStatusAsync(Guid statusId, CancellationToken cancellationToken = default) =>
        await DbContext.ContasReceber.AsNoTracking()
            .Where(c => c.StatusContaId == statusId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ContaReceber>> ListByPagadorAsync(Guid pagadorId, CancellationToken cancellationToken = default) =>
        await DbContext.ContasReceber.AsNoTracking()
            .Where(c => c.PagadorId == pagadorId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ContaReceber>> ListByGrupoParcelamentoAsync(Guid grupoParcelamentoId, CancellationToken cancellationToken = default) =>
        await DbContext.ContasReceber.AsNoTracking()
            .Where(c => c.GrupoParcelamentoId == grupoParcelamentoId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ContaReceber>> ListByDataVencimentoAsync(DateOnly dataInicio, DateOnly dataFim, CancellationToken cancellationToken = default) =>
        await DbContext.ContasReceber.AsNoTracking()
            .Where(c => c.DataVencimento >= dataInicio && c.DataVencimento <= dataFim)
            .ToListAsync(cancellationToken);

    public Task AddRangeAsync(IReadOnlyCollection<ContaReceber> contas, CancellationToken cancellationToken = default)
    {
        DbContext.ContasReceber.AddRange(contas);
        return Task.CompletedTask;
    }
}
