using ControleFinanceiro.Application.FinanceAI;
using ControleFinanceiro.Infrastructure.FinanceAI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ControleFinanceiro.Infrastructure.Alertas;

public sealed class AlertasEmailPushHostedService(
    IServiceProvider serviceProvider,
    IOptions<AlertasWhatsappOptions> options,
    ILogger<AlertasEmailPushHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Serviço de alertas email/push iniciado. Horário: {Hora:00}:{Minuto:00}.",
            options.Value.HoraExecucao, options.Value.MinutoExecucao);

        while (!stoppingToken.IsCancellationRequested)
        {
            var agora = DateTime.Now;
            var proximaExecucao = ProximaExecucao(agora);
            var espera = proximaExecucao - agora;

            logger.LogDebug("Próxima execução de alertas email/push em {Espera:hh\\:mm\\:ss}", espera);

            try
            {
                await Task.Delay(espera, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await ExecutarAlertasAsync(stoppingToken);
        }
    }

    private async Task ExecutarAlertasAsync(CancellationToken ct)
    {
        try
        {
            await ControleFinanceiro.Application.Common.WorkspaceJobRunner.RunAsync<AlertasEmailPushService>(
                    serviceProvider.GetRequiredService<IServiceScopeFactory>(), (service, token) => service.ProcessarAsync(token), logger, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao processar alertas email/push.");
        }
    }

    // Executa 5 minutos depois do horário base do WhatsApp para não sobrepor
    private DateTime ProximaExecucao(DateTime agora)
    {
        var opts = options.Value;
        var candidato = agora.Date.AddHours(opts.HoraExecucao).AddMinutes(opts.MinutoExecucao + 5);
        return candidato > agora ? candidato : candidato.AddDays(1);
    }
}
