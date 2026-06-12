using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Cadastros.Pessoas;

namespace ControleFinanceiro.Application.Cadastros.Pessoas;

public interface IPessoaRepository : IRepository<Pessoa>
{
    Task<Pessoa?> GetByIdWithChavesPixAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Pessoa>> ListAtivasAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Pessoa>> ListByTipoAsync(TipoPessoa tipoPessoa, CancellationToken cancellationToken = default);
    Task<Pessoa?> GetByCpfCnpjAsync(string cpfCnpj, CancellationToken cancellationToken = default);
}