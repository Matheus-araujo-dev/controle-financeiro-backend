using ControleFinanceiro.Contracts.Financeiro.ContasPagar;

namespace ControleFinanceiro.Application.Financeiro.ContasPagar;

public interface IContaPagarQueryService
{
    Task<ContaPagarListResponse> ListarAsync(
        ContaPagarListQueryRequest query,
        CancellationToken cancellationToken);

    Task<ContaPagarDetalheResponse?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);
}