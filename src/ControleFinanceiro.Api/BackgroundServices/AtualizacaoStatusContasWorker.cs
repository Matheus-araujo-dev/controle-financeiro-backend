using ControleFinanceiro.Application.Financeiro.Status;

namespace ControleFinanceiro.Api.BackgroundServices;

/// <summary>
/// Mantém o status das contas a pagar/receber em dia: a cada ciclo marca como "Vencida" as contas
/// pendentes cujo vencimento já passou. Roda na inicialização e a cada 12h (≥ diariamente), de forma
/// idempotente — contas que vencem ao longo do dia são corrigidas no próximo ciclo, e a listagem já
/// exibe o status efetivo no intervalo.
/// </summary>
public sealed class AtualizacaoStatusContasWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<AtualizacaoStatusContasWorker> logger) : BackgroundService
{
    private static readonly TimeSpan IntervaloVerificacao = TimeSpan.FromHours(12);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Trabalhador de Atualização de Status de Contas iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<AtualizacaoStatusContasService>();

                await service.MarcarContasVencidasAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro ao atualizar o status das contas vencidas.");
            }

            try
            {
                await Task.Delay(IntervaloVerificacao, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Trabalhador de Atualização de Status de Contas finalizado.");
    }
}
