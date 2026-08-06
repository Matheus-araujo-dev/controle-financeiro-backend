using ControleFinanceiro.Contracts.Financeiro.Common;
using ControleFinanceiro.Contracts.Financeiro.ContasReceber;

namespace ControleFinanceiro.Application.Financeiro.ContasReceber;

public interface IContaReceberQueryService
{
    Task<ContaReceberListResponse> ListarAsync(ContaReceberListQueryRequest query, CancellationToken cancellationToken);
    Task<ContaReceberDetalheResponse?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<HistoricoEntradaResponse>> ObterHistoricoAsync(Guid id, CancellationToken cancellationToken);
}
