namespace ControleFinanceiro.Application.Common.Alertas;

public interface IEmailAlertaService
{
    Task<bool> EnviarAsync(string destinatario, string assunto, string htmlBody, CancellationToken cancellationToken);
}
