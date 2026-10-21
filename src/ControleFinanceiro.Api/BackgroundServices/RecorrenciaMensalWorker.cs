using ControleFinanceiro.Application.Financeiro.Recorrencias;

namespace ControleFinanceiro.Api.BackgroundServices;

public sealed class RecorrenciaMensalWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<RecorrenciaMensalWorker> logger) : BackgroundService
{
    private static readonly TimeSpan IntervaloVerificacao = TimeSpan.FromHours(4);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Trabalhador de Recorrência Mensal iniciado.");

        // Roda na inicialização e a cada intervalo, não apenas no dia 1: a geração é
        // idempotente (materializa só datas pendentes desde o início da regra), então
        // meses perdidos com o servidor desligado são recuperados automaticamente.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ControleFinanceiro.Application.Common.WorkspaceJobRunner.RunAsync<RecorrenciaAppService>(
                    scopeFactory, (service, token) => service.GerarOcorrenciasRecorrentesNoMesAsync(DateOnly.FromDateTime(DateTime.Now), token), logger, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro fatal ao processar geração automática de recorrência mensal.");
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

        logger.LogInformation("Trabalhador de Recorrência Mensal finalizado.");
    }
}
