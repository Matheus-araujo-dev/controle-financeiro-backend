using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Financeiro;

namespace ControleFinanceiro.Application.Financeiro.ContasReceber;

public interface IContaReceberRepository : IRepository<ContaReceber>
{
    Task<ContaReceber?> GetByIdWithRateiosAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContaReceber>> ListByStatusAsync(Guid statusId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContaReceber>> ListByPagadorAsync(Guid pagadorId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContaReceber>> ListByGrupoParcelamentoAsync(Guid grupoParcelamentoId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContaReceber>> ListByDataVencimentoAsync(DateOnly dataInicio, DateOnly dataFim, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IReadOnlyCollection<ContaReceber> contas, CancellationToken cancellationToken = default);
}