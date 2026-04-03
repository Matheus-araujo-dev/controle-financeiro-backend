using Microsoft.Extensions.DependencyInjection;
using ControleFinanceiro.Application.Bootstrap;

namespace ControleFinanceiro.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IBootstrapCatalogService, BootstrapCatalogService>();
        return services;
    }
}
