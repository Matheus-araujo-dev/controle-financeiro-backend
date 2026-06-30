using ControleFinanceiro.Application.Cadastros.Pessoas;
using ControleFinanceiro.Domain.Cadastros.Pessoas;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Infrastructure.Persistence.Repositories;

public sealed class PessoaRepository(AppDbContext dbContext)
    : GenericRepository<Pessoa>(dbContext), IPessoaRepository
{
    public async Task<Pessoa?> GetByIdWithChavesPixAsync(Guid id, CancellationToken cancellationToken = default) =>
        await DbContext.Pessoas
            .Include(p => p.ChavesPix)
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Pessoa>> ListAtivasAsync(CancellationToken cancellationToken = default) =>
        await DbContext.Pessoas.AsNoTracking()
            .Where(p => p.Ativo)
            .OrderBy(p => p.Nome)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Pessoa>> ListByTipoAsync(TipoPessoa tipoPessoa, CancellationToken cancellationToken = default) =>
        await DbContext.Pessoas.AsNoTracking()
            .Where(p => p.TipoPessoa == tipoPessoa)
            .OrderBy(p => p.Nome)
            .ToListAsync(cancellationToken);

    public async Task<Pessoa?> GetByCpfCnpjAsync(string cpfCnpj, CancellationToken cancellationToken = default) =>
        await DbContext.Pessoas.AsNoTracking()
            .SingleOrDefaultAsync(p => p.CpfCnpj == cpfCnpj, cancellationToken);
}
