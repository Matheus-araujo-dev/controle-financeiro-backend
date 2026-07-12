using System.Text;
using ControleFinanceiro.Application.Common.Alertas;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.FinanceAI;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ControleFinanceiro.Application.FinanceAI;

public sealed class AlertasEmailPushService(
    IAppDbContext db,
    IEmailAlertaService email,
    IPushAlertaService push,
    ILogger<AlertasEmailPushService> logger)
{
    public async Task ProcessarAsync(CancellationToken cancellationToken)
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        logger.LogInformation("Processando alertas email/push para {Hoje}", hoje);

        await ProcessarVencimentosAsync(hoje, cancellationToken);
        await ProcessarLimitesCategoriaAsync(hoje, cancellationToken);
    }

    // ─── Alertas de vencimento ────────────────────────────────────────────────

    private async Task ProcessarVencimentosAsync(DateOnly hoje, CancellationToken ct)
    {
        var usuariosEmail = await (
            from cfg in db.ConfiguracoesNotificacao
            join u in db.Usuarios on cfg.UsuarioId equals u.Id
            where cfg.EmailAtivo && cfg.EmailVencimento
            select new
            {
                cfg.UsuarioId,
                cfg.FamiliaId,
                cfg.EmailDestinatario,
                cfg.EmailDiasAntecedencia,
                u.Nome
            })
            .AsNoTracking()
            .ToListAsync(ct);

        var usuariosPush = await (
            from cfg in db.ConfiguracoesNotificacao
            join sub in db.PushSubscriptions on cfg.UsuarioId equals sub.UsuarioId
            where cfg.PushAtivo && cfg.PushVencimento && sub.Ativo
            select new
            {
                cfg.UsuarioId,
                cfg.FamiliaId,
                cfg.PushDiasAntecedencia,
                sub.Endpoint,
                sub.P256dh,
                sub.Auth,
                SubscriptionId = sub.Id
            })
            .AsNoTracking()
            .ToListAsync(ct);

        // Email
        foreach (var u in usuariosEmail.Where(x => !string.IsNullOrWhiteSpace(x.EmailDestinatario)))
        {
            var dataAlvo = hoje.AddDays(u.EmailDiasAntecedencia);
            var chave = dataAlvo.ToString("yyyyMMdd");

            if (await JaEnviouAsync(u.UsuarioId, AlertaDigitalEnviado.CanalEmail, AlertaDigitalEnviado.TipoVencimento, chave, hoje, ct))
                continue;

            var contas = await BuscarContasVencendoAsync(u.FamiliaId, dataAlvo, ct);
            if (contas.Count == 0) continue;

            var html = ConstruirHtmlVencimento(contas, dataAlvo, u.EmailDiasAntecedencia);
            var enviado = await email.EnviarAsync(u.EmailDestinatario!, $"🔔 Vencimento em {dataAlvo:dd/MM/yyyy}", html, ct);
            if (enviado)
                await RegistrarEnvioAsync(u.UsuarioId, AlertaDigitalEnviado.CanalEmail, AlertaDigitalEnviado.TipoVencimento, chave, hoje, ct);
        }

        // Push
        foreach (var u in usuariosPush)
        {
            var dataAlvo = hoje.AddDays(u.PushDiasAntecedencia);
            var chave = $"{u.SubscriptionId}:{dataAlvo:yyyyMMdd}";

            if (await JaEnviouAsync(u.UsuarioId, AlertaDigitalEnviado.CanalPush, AlertaDigitalEnviado.TipoVencimento, chave, hoje, ct))
                continue;

            var contas = await BuscarContasVencendoAsync(u.FamiliaId, dataAlvo, ct);
            if (contas.Count == 0) continue;

            var total = contas.Sum(c => c.ValorLiquido);
            var diasTexto = u.PushDiasAntecedencia == 1 ? "amanhã" : $"em {u.PushDiasAntecedencia} dias";
            var corpo = $"{contas.Count} conta(s) vencendo {diasTexto} — Total: R$ {total:N2}";

            // Create a lightweight PushSubscription instance for the service interface
            var sub = CriarPushSubscriptionTemp(u.SubscriptionId, u.UsuarioId, u.FamiliaId, u.Endpoint, u.P256dh, u.Auth);
            var enviado = await push.EnviarAsync(sub, $"🔔 Vencimento {dataAlvo:dd/MM}", corpo, ct);
            if (enviado)
                await RegistrarEnvioAsync(u.UsuarioId, AlertaDigitalEnviado.CanalPush, AlertaDigitalEnviado.TipoVencimento, chave, hoje, ct);
        }
    }

    // ─── Alertas de limite de categoria ──────────────────────────────────────

    private async Task ProcessarLimitesCategoriaAsync(DateOnly hoje, CancellationToken ct)
    {
        var competencia = hoje.ToString("yyyy-MM");
        var inicioMes = new DateOnly(hoje.Year, hoje.Month, 1);
        var fimMes = inicioMes.AddMonths(1).AddDays(-1);

        var usuariosEmail = await (
            from cfg in db.ConfiguracoesNotificacao
            where cfg.EmailAtivo && cfg.EmailLimiteCategoria && cfg.EmailDestinatario != null
            select new { cfg.UsuarioId, cfg.FamiliaId, cfg.EmailDestinatario })
            .AsNoTracking()
            .ToListAsync(ct);

        var usuariosPush = await (
            from cfg in db.ConfiguracoesNotificacao
            join sub in db.PushSubscriptions on cfg.UsuarioId equals sub.UsuarioId
            where cfg.PushAtivo && cfg.PushLimiteCategoria && sub.Ativo
            select new { cfg.UsuarioId, cfg.FamiliaId, sub.Endpoint, sub.P256dh, sub.Auth, SubscriptionId = sub.Id })
            .AsNoTracking()
            .ToListAsync(ct);

        var familiaIdsEmail = usuariosEmail.Select(u => u.FamiliaId).Distinct().ToList();
        var familiaIdsPush = usuariosPush.Select(u => u.FamiliaId).Distinct().ToList();
        var todasFamilias = familiaIdsEmail.Union(familiaIdsPush).Distinct().ToList();

        foreach (var familiaId in todasFamilias)
        {
            var metas = await (
                from m in db.MetasOrcamento
                join cg in db.ContasGerenciais on m.ContaGerencialId equals cg.Id
                where m.FamiliaId == familiaId && m.Competencia == competencia
                select new { m.ContaGerencialId, m.ValorMeta, CategoriaNome = cg.Descricao })
                .AsNoTracking()
                .ToListAsync(ct);

            if (metas.Count == 0) continue;

            var realizados = await (
                from r in db.RateiosContaGerencial
                join cp in db.ContasPagar on r.ContaPagarId equals cp.Id
                where r.FamiliaId == familiaId
                   && r.ContaPagarId.HasValue
                   && cp.StatusContaId != StatusConta.CanceladaId
                   && cp.DataVencimento >= inicioMes
                   && cp.DataVencimento <= fimMes
                group r.Valor by r.ContaGerencialId into g
                select new { ContaGerencialId = g.Key, Total = g.Sum() })
                .AsNoTracking()
                .ToListAsync(ct);

            var realizadoMap = realizados.ToDictionary(x => x.ContaGerencialId, x => x.Total);

            foreach (var meta in metas)
            {
                var realizado = realizadoMap.GetValueOrDefault(meta.ContaGerencialId, 0m);
                var percentual = meta.ValorMeta > 0 ? realizado / meta.ValorMeta : 0m;
                if (percentual < 0.90m) continue;

                var chaveBase = $"{meta.ContaGerencialId}:{competencia}";
                var emoji = percentual >= 1m ? "🚨" : "⚠️";
                var pct = (percentual * 100m).ToString("N0");

                // Email
                foreach (var u in usuariosEmail.Where(x => x.FamiliaId == familiaId))
                {
                    var chave = $"email:{chaveBase}";
                    if (await JaEnviouAsync(u.UsuarioId, AlertaDigitalEnviado.CanalEmail, AlertaDigitalEnviado.TipoLimiteCategoria, chave, hoje, ct))
                        continue;

                    var html = $"<p>{emoji} <strong>Alerta de orçamento — {meta.CategoriaNome}</strong></p>" +
                               $"<p>Realizado: R$ {realizado:N2} de R$ {meta.ValorMeta:N2} ({pct}%)</p>" +
                               $"<p>Competência: {competencia}</p>";

                    var enviado = await email.EnviarAsync(u.EmailDestinatario!, $"{emoji} Orçamento — {meta.CategoriaNome}", html, ct);
                    if (enviado)
                        await RegistrarEnvioAsync(u.UsuarioId, AlertaDigitalEnviado.CanalEmail, AlertaDigitalEnviado.TipoLimiteCategoria, chave, hoje, ct);
                }

                // Push
                foreach (var u in usuariosPush.Where(x => x.FamiliaId == familiaId))
                {
                    var chave = $"push:{u.SubscriptionId}:{chaveBase}";
                    if (await JaEnviouAsync(u.UsuarioId, AlertaDigitalEnviado.CanalPush, AlertaDigitalEnviado.TipoLimiteCategoria, chave, hoje, ct))
                        continue;

                    var corpo = $"Realizado: R$ {realizado:N2} de R$ {meta.ValorMeta:N2} ({pct}%)";
                    var sub = CriarPushSubscriptionTemp(u.SubscriptionId, u.UsuarioId, u.FamiliaId, u.Endpoint, u.P256dh, u.Auth);
                    var enviado = await push.EnviarAsync(sub, $"{emoji} {meta.CategoriaNome}", corpo, ct);
                    if (enviado)
                        await RegistrarEnvioAsync(u.UsuarioId, AlertaDigitalEnviado.CanalPush, AlertaDigitalEnviado.TipoLimiteCategoria, chave, hoje, ct);
                }
            }
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private async Task<List<(string Descricao, decimal ValorLiquido, string? RecebedorNome)>> BuscarContasVencendoAsync(
        Guid familiaId, DateOnly dataAlvo, CancellationToken ct)
    {
        var result = await (
            from cp in db.ContasPagar
            join p in db.Pessoas on cp.RecebedorId equals p.Id into pj
            from p in pj.DefaultIfEmpty()
            where cp.FamiliaId == familiaId
               && cp.DataVencimento == dataAlvo
               && cp.StatusContaId == StatusConta.PendenteId
            select new
            {
                cp.Descricao,
                cp.ValorLiquido,
                RecebedorNome = p != null ? p.Nome : null
            })
            .AsNoTracking()
            .ToListAsync(ct);

        return result.Select(r => (r.Descricao, r.ValorLiquido, (string?)r.RecebedorNome)).ToList();
    }

    private static string ConstruirHtmlVencimento(
        List<(string Descricao, decimal ValorLiquido, string? RecebedorNome)> contas,
        DateOnly dataAlvo,
        int diasAntecedencia)
    {
        var total = contas.Sum(c => c.ValorLiquido);
        var diasTexto = diasAntecedencia == 1 ? "amanhã" : $"em {diasAntecedencia} dias";

        var sb = new StringBuilder();
        sb.Append($"<h2>🔔 Lembrete de vencimento — {dataAlvo:dd/MM/yyyy} ({diasTexto})</h2><ul>");
        foreach (var c in contas)
        {
            var recebedor = c.RecebedorNome is not null ? $" → {c.RecebedorNome}" : string.Empty;
            sb.Append($"<li>{c.Descricao}{recebedor}: <strong>R$ {c.ValorLiquido:N2}</strong></li>");
        }
        sb.Append($"</ul><p><strong>Total: R$ {total:N2}</strong></p>");
        return sb.ToString();
    }

    private Task<bool> JaEnviouAsync(
        Guid usuarioId, string canal, string tipo, string chave, DateOnly dataEnvio, CancellationToken ct) =>
        db.AlertasDigitaisEnviados.AnyAsync(
            a => a.UsuarioId == usuarioId
              && a.Canal == canal
              && a.TipoAlerta == tipo
              && a.ChaveReferencia == chave
              && a.DataEnvio == dataEnvio,
            ct);

    private async Task RegistrarEnvioAsync(
        Guid usuarioId, string canal, string tipo, string chave, DateOnly dataEnvio, CancellationToken ct)
    {
        db.AlertasDigitaisEnviados.Add(
            AlertaDigitalEnviado.Registrar(usuarioId, canal, tipo, chave, dataEnvio));
        await db.SaveChangesAsync(ct);
    }

    // Cria uma instância temporária de PushSubscription apenas para passar ao serviço
    private static PushSubscription CriarPushSubscriptionTemp(
        Guid id, Guid usuarioId, Guid familiaId, string endpoint, string p256dh, string auth)
    {
        var sub = PushSubscription.Registrar(familiaId, usuarioId, endpoint, p256dh, auth);
        return sub;
    }
}
