using ControleFinanceiro.Contracts.Dashboard;
using MediatR;

namespace ControleFinanceiro.Application.Dashboard.Queries;

public sealed class GetDashboardResumoQueryHandler(IDashboardResumoService resumoService)
    : IRequestHandler<GetDashboardResumoQuery, DashboardResumoResponse>
{
    public Task<DashboardResumoResponse> Handle(
        GetDashboardResumoQuery request,
        CancellationToken cancellationToken)
        => resumoService.ObterResumoAsync(request.Request, cancellationToken);
}
