using ControleFinanceiro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Api.BackgroundServices;

public sealed class MigrationStartupService(
    IServiceScopeFactory scopeFactory,
    ILogger<MigrationStartupService> logger,
    IHostApplicationLifetime lifetime) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        lifetime.ApplicationStarted.Register(() =>
        {
            _ = Task.Run(() => RunMigrationsAsync(cancellationToken), cancellationToken);
        });
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task RunMigrationsAsync(CancellationToken cancellationToken)
    {
        const int maxAttempts = 5;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var pending = await db.Database.GetPendingMigrationsAsync(cancellationToken);
                var pendingList = pending.ToArray();
                if (pendingList.Length == 0)
                {
                    logger.LogInformation("Nenhuma migration pendente.");
                    return;
                }
                logger.LogInformation("Aplicando {Count} migration(s): {Names}",
                    pendingList.Length, string.Join(", ", pendingList));
                await db.Database.MigrateAsync(cancellationToken);
                logger.LogInformation("Migrations aplicadas com sucesso.");
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                logger.LogWarning(ex, "Tentativa {Attempt}/{Max} de migration falhou. Aguardando 5s...",
                    attempt, maxAttempts);
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha ao aplicar migrations após {Max} tentativas.", maxAttempts);
                throw;
            }
        }
    }
}
