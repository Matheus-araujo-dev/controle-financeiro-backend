using ControleFinanceiro.Application.FinanceAI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ControleFinanceiro.Infrastructure.FinanceAI;

/// <summary>
/// Executa os alertas de vencimento WhatsApp uma vez por dia no horário configurado.
/// </summary>
public sealed class AlertasWhatsappHostedService(
    IServiceProvider serviceProvider,
    IOptions<AlertasWhatsappOptions> options,
    ILogger<AlertasWhatsappHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Serviço de alertas WhatsApp iniciado. Horário: {Hora:00}:{Minuto:00}.",
            options.Value.HoraExecucao, options.Value.MinutoExecucao);

        while (!stoppingToken.IsCancellationRequested)
        {
            var agora = DateTime.Now;
            var proximaExecucao = ProximaExecucao(agora);
            var espera = proximaExecucao - agora;

            logger.LogDebug("Próxima execução de alertas em {Espera:hh\\:mm\\:ss} ({ProximaExecucao:g})",
                espera, proximaExecucao);

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
            using var scope = serviceProvider.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<AlertasVencimentoService>();
            await service.ProcessarAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao processar alertas de vencimento WhatsApp.");
        }
    }

    private DateTime ProximaExecucao(DateTime agora)
    {
        var opts = options.Value;
        var candidato = agora.Date.AddHours(opts.HoraExecucao).AddMinutes(opts.MinutoExecucao);
        // Se o horário de hoje já passou, agenda para amanhã
        return candidato > agora ? candidato : candidato.AddDays(1);
    }
}

public sealed class AlertasWhatsappOptions
{
    public const string SectionName = "AlertasWhatsapp";

    /// <summary>Hora do dia para executar (0–23). Padrão: 8.</summary>
    public int HoraExecucao { get; set; } = 8;

    /// <summary>Minuto para executar (0–59). Padrão: 0.</summary>
    public int MinutoExecucao { get; set; } = 0;
}
