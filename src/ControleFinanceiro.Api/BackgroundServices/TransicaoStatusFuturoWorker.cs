using ControleFinanceiro.Application.Financeiro.Status;

namespace ControleFinanceiro.Api.BackgroundServices;

/// <summary>
/// Transiciona para "Pendente" as parcelas com status "Futuro" cujo mês de vencimento chegou.
/// Roda na inicialização e a cada 12h, de forma idempotente — cobre todas as famílias.
/// </summary>
public sealed class TransicaoStatusFuturoWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<TransicaoStatusFuturoWorker> logger) : BackgroundService
{
    private static readonly TimeSpan IntervaloVerificacao = TimeSpan.FromHours(4);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Trabalhador de Transição de Status Futuro iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ControleFinanceiro.Application.Common.WorkspaceJobRunner.RunAsync<TransicaoStatusFuturoService>(
                    scopeFactory, (service, token) => service.TransicionarFuturoParaPendenteAsync(token), logger, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro ao processar transição de status FUTURO→PENDENTE.");
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

        logger.LogInformation("Trabalhador de Transição de Status Futuro finalizado.");
    }
}
