using ControleFinanceiro.Contracts.Bootstrap;
using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Contracts.Filters;

namespace ControleFinanceiro.Application.Bootstrap;

public sealed class BootstrapCatalogService : IBootstrapCatalogService
{
    private static readonly BootstrapModuleItemResponse[] Modules =
    [
        new("dashboard", "Dashboard", "/dashboard", 6),
        new("pessoas", "Pessoas", "/pessoas", 2),
        new("formas-pagamento", "Formas de pagamento", "/formas-pagamento", 2),
        new("contas-bancarias", "Contas bancarias", "/contas-bancarias", 2),
        new("cartoes", "Cartoes", "/cartoes", 2),
        new("contas-gerenciais", "Contas gerenciais", "/contas-gerenciais", 2),
        new("contas-pagar", "Contas a pagar", "/contas-pagar", 3),
        new("contas-receber", "Contas a receber", "/contas-receber", 3),
        new("movimentacoes", "Movimentacoes", "/movimentacoes", 3),
        new("faturas", "Faturas", "/faturas", 4),
        new("importacoes-whatsapp", "Importacoes WhatsApp", "/importacoes-whatsapp", 7),
        new("conciliacao", "Conciliacao", "/conciliacao", 8)
    ];

    public PagedResult<BootstrapModuleItemResponse> ListModules(ListQueryRequest query)
    {
        var normalizedSearch = query.Search?.Trim();

        var filtered = Modules
            .Where(module =>
                string.IsNullOrWhiteSpace(normalizedSearch) ||
                module.Name.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                module.Route.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                module.Code.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
            .OrderBy(module => module.Phase)
            .ThenBy(module => module.Name)
            .ToArray();

        var page = query.NormalizedPage;
        var pageSize = query.NormalizedPageSize;
        var items = filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArray();

        return PagedResult<BootstrapModuleItemResponse>.Create(items, page, pageSize, filtered.Length);
    }
}
