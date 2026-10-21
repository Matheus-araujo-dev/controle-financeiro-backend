using ControleFinanceiro.Domain.FinanceAI;

namespace ControleFinanceiro.Application.Common.Alertas;

public interface IPushAlertaService
{
    string GetVapidPublicKey();
    Task<bool> EnviarAsync(PushSubscription subscription, string titulo, string corpo, CancellationToken cancellationToken);
}
