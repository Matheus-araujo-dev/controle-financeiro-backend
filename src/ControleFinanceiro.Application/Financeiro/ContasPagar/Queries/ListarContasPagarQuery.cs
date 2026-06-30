using ControleFinanceiro.Contracts.Financeiro.ContasPagar;
using MediatR;

namespace ControleFinanceiro.Application.Financeiro.ContasPagar.Queries;

public sealed record ListarContasPagarQuery(ContaPagarListQueryRequest Request)
    : IRequest<ContaPagarListResponse>;
