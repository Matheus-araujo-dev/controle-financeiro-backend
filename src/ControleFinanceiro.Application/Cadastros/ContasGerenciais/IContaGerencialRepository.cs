using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;

namespace ControleFinanceiro.Application.Cadastros.ContasGerenciais;

public interface IContaGerencialRepository : IRepository<ContaGerencial>
{
    Task<IReadOnlyList<ContaGerencial>> ListAtivasAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContaGerencial>> ListByTipoAsync(TipoContaGerencial tipo, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContaGerencial>> ListByContaPaiAsync(Guid? contaPaiId, CancellationToken cancellationToken = default);
    Task<ContaGerencial?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default);
}