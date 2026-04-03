using ControleFinanceiro.Contracts.Bootstrap;
using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Contracts.Filters;

namespace ControleFinanceiro.Application.Bootstrap;

public interface IBootstrapCatalogService
{
    PagedResult<BootstrapModuleItemResponse> ListModules(ListQueryRequest query);
}
