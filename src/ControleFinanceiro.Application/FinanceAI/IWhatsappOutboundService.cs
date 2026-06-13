namespace ControleFinanceiro.Application.FinanceAI;

public interface IWhatsappOutboundService
{
    Task EnviarAsync(string telefone, string texto, CancellationToken cancellationToken);
}
