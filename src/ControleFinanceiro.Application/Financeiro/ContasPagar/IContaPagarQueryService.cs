using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Contracts.Financeiro.ContasPagar;

namespace ControleFinanceiro.Application.Financeiro.ContasPagar;

public interface IContaPagarQueryService
{
    Task<ContaPagarListResponse> ListarAsync(
        ContaPagarListQueryRequest query,
        CancellationToken cancellationToken);

    Task<CursorPagedResult<ContaPagarResumoResponse>> ListarCursorAsync(
        ContaPagarCursorQueryRequest query,
        CancellationToken cancellationToken);

    Task<ContaPagarDetalheResponse?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);
}