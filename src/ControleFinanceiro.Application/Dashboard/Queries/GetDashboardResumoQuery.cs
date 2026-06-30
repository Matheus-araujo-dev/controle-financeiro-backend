using ControleFinanceiro.Contracts.Dashboard;
using MediatR;

namespace ControleFinanceiro.Application.Dashboard.Queries;

public sealed record GetDashboardResumoQuery(DashboardResumoQueryRequest Request)
    : IRequest<DashboardResumoResponse>;
