using ControleFinanceiro.Contracts.Financeiro.ContasPagar;
using MediatR;

namespace ControleFinanceiro.Application.Financeiro.ContasPagar.Queries;

public sealed class ListarContasPagarQueryHandler(IContaPagarQueryService queryService)
    : IRequestHandler<ListarContasPagarQuery, ContaPagarListResponse>
{
    public Task<ContaPagarListResponse> Handle(
        ListarContasPagarQuery request,
        CancellationToken cancellationToken)
        => queryService.ListarAsync(request.Request, cancellationToken);
}
