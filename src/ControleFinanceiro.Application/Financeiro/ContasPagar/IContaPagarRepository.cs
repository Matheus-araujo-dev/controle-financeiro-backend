using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Financeiro;

namespace ControleFinanceiro.Application.Financeiro.ContasPagar;

public interface IContaPagarRepository : IRepository<ContaPagar>
{
    Task<ContaPagar?> GetByIdWithRateiosAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContaPagar>> ListByStatusAsync(Guid statusId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContaPagar>> ListByRecebedorAsync(Guid recebedorId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContaPagar>> ListByGrupoParcelamentoAsync(Guid grupoParcelamentoId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContaPagar>> ListByDataVencimentoAsync(DateOnly dataInicio, DateOnly dataFim, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IReadOnlyCollection<ContaPagar> contas, CancellationToken cancellationToken = default);
}