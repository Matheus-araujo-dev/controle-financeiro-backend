using ControleFinanceiro.Api.Configuration;
using ControleFinanceiro.Application.Common.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ControleFinanceiro.Api.Tests.Infrastructure;

internal static class WorkspaceTestScope
{
    public static AsyncServiceScope CreateWorkspaceAsyncScope(this IServiceProvider services)
    {
        var scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IAppDbContext>().DefinirWorkspaceCorrente(
            services.GetRequiredService<IOptions<AuthOptions>>().Value.DevelopmentFamiliaId);
        return scope;
    }

    // Fixtures de servicos/repositorios devem reproduzir o workspace recebido no HTTP.
    public static IServiceScope CreateWorkspaceScope(this IServiceProvider services)
    {
        var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IAppDbContext>().DefinirWorkspaceCorrente(
            services.GetRequiredService<IOptions<AuthOptions>>().Value.DevelopmentFamiliaId);
        return scope;
    }
}
