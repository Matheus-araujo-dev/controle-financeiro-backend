using ControleFinanceiro.Domain.FinanceAI;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Common.Persistence;

public interface IWhatsappDbContext
{
    DbSet<WhatsappUsuario> WhatsappUsuarios { get; }
    DbSet<WhatsappConfigAlerta> WhatsappConfigAlertas { get; }
    DbSet<AlertaWhatsappEnviado> AlertasWhatsappEnviados { get; }
    DbSet<ConfiguracaoNotificacao> ConfiguracoesNotificacao { get; }
    DbSet<PushSubscription> PushSubscriptions { get; }
    DbSet<AlertaDigitalEnviado> AlertasDigitaisEnviados { get; }
}
