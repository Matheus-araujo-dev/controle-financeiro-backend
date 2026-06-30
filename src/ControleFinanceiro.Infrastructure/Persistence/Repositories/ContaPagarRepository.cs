using ControleFinanceiro.Application.Financeiro.ContasPagar;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Infrastructure.Persistence.Repositories;

public sealed class ContaPagarRepository(AppDbContext dbContext)
    : GenericRepository<ContaPagar>(dbContext), IContaPagarRepository
{
    public async Task<ContaPagar?> GetByIdWithRateiosAsync(Guid id, CancellationToken cancellationToken = default) =>
        await DbContext.ContasPagar
            .Include(c => c.Rateios)
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ContaPagar>> ListByStatusAsync(Guid statusId, CancellationToken cancellationToken = default) =>
        await DbContext.ContasPagar.AsNoTracking()
            .Where(c => c.StatusContaId == statusId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ContaPagar>> ListByRecebedorAsync(Guid recebedorId, CancellationToken cancellationToken = default) =>
        await DbContext.ContasPagar.AsNoTracking()
            .Where(c => c.RecebedorId == recebedorId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ContaPagar>> ListByGrupoParcelamentoAsync(Guid grupoParcelamentoId, CancellationToken cancellationToken = default) =>
        await DbContext.ContasPagar.AsNoTracking()
            .Where(c => c.GrupoParcelamentoId == grupoParcelamentoId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ContaPagar>> ListByDataVencimentoAsync(DateOnly dataInicio, DateOnly dataFim, CancellationToken cancellationToken = default) =>
        await DbContext.ContasPagar.AsNoTracking()
            .Where(c => c.DataVencimento >= dataInicio && c.DataVencimento <= dataFim)
            .ToListAsync(cancellationToken);

    public Task AddRangeAsync(IReadOnlyCollection<ContaPagar> contas, CancellationToken cancellationToken = default)
    {
        DbContext.ContasPagar.AddRange(contas);
        return Task.CompletedTask;
    }
}
