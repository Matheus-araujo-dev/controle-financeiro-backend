using ControleFinanceiro.Contracts.Financeiro.ContasPagar;

namespace ControleFinanceiro.Application.Financeiro.ContasPagar;

public interface IContaPagarAppService : IContaPagarQueryService, IContaPagarCommandService
{
}