using ControleFinanceiro.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ControleFinanceiro.Application.Common;

/// <summary>Executa jobs internos em escopos separados; nenhuma consulta financeira recebe acesso global.</summary>
public static class WorkspaceJobRunner
{
    public static async Task RunAsync<TService>(
        IServiceScopeFactory scopeFactory,
        Func<TService, CancellationToken, Task> execute,
        ILogger logger,
        CancellationToken cancellationToken) where TService : notnull
    {
        Guid[] workspaceIds;
        using (var discovery = scopeFactory.CreateScope())
        {
            var db = discovery.ServiceProvider.GetRequiredService<IAppDbContext>();
            workspaceIds = await db.Familias.AsNoTracking().Select(f => f.Id).ToArrayAsync(cancellationToken);
        }

        foreach (var workspaceId in workspaceIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
                db.DefinirWorkspaceCorrente(workspaceId);
                await execute(scope.ServiceProvider.GetRequiredService<TService>(), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Job {Job} falhou no workspace {WorkspaceId}.", typeof(TService).Name, workspaceId);
            }
        }
    }
}
