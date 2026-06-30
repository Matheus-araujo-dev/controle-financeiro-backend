using ControleFinanceiro.Application.Cadastros.ContasGerenciais;
using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Infrastructure.Persistence.Repositories;

public sealed class ContaGerencialRepository(AppDbContext dbContext)
    : GenericRepository<ContaGerencial>(dbContext), IContaGerencialRepository
{
    public async Task<IReadOnlyList<ContaGerencial>> ListAtivasAsync(CancellationToken cancellationToken = default) =>
        await DbContext.ContasGerenciais.AsNoTracking()
            .Where(c => c.Ativo)
            .OrderBy(c => c.Codigo)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ContaGerencial>> ListByTipoAsync(TipoContaGerencial tipo, CancellationToken cancellationToken = default) =>
        await DbContext.ContasGerenciais.AsNoTracking()
            .Where(c => c.Tipo == tipo)
            .OrderBy(c => c.Codigo)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ContaGerencial>> ListByContaPaiAsync(Guid? contaPaiId, CancellationToken cancellationToken = default) =>
        await DbContext.ContasGerenciais.AsNoTracking()
            .Where(c => c.ContaPaiId == contaPaiId)
            .OrderBy(c => c.Codigo)
            .ToListAsync(cancellationToken);

    public async Task<ContaGerencial?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default) =>
        await DbContext.ContasGerenciais.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Codigo == codigo, cancellationToken);
}
