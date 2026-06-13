using System.Text;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.FinanceAI;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ControleFinanceiro.Application.FinanceAI;

public sealed class AlertasVencimentoService(
    IAppDbContext db,
    IWhatsappOutboundService outbound,
    ILogger<AlertasVencimentoService> logger)
{
    public async Task ProcessarAsync(CancellationToken cancellationToken)
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        logger.LogInformation("Processando alertas para {Hoje}", hoje);

        await ProcessarVencimentosAsync(hoje, cancellationToken);
        await ProcessarLimitesCategoriaAsync(hoje, cancellationToken);
    }

    // ─── Alertas de vencimento ───────────────────────────────────────────────

    private async Task ProcessarVencimentosAsync(DateOnly hoje, CancellationToken cancellationToken)
    {
        var usuariosAlerta = await (
            from wup in db.WhatsappUsuarios
            join cfg in db.WhatsappConfigAlertas on wup.UsuarioId equals cfg.UsuarioId
            where wup.Ativo && cfg.ReceberVencimento
            select new
            {
                wup.Telefone,
                wup.FamiliaId,
                cfg.DiasAntecedenciaVencimento
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        foreach (var u in usuariosAlerta)
        {
            var dataAlvo = hoje.AddDays(u.DiasAntecedenciaVencimento);
            var chave = dataAlvo.ToString("yyyyMMdd");

            if (await JaEnviouAsync(u.Telefone, AlertaWhatsappEnviado.TipoVencimento, chave, hoje, cancellationToken))
                continue;

            var contas = await (
                from cp in db.ContasPagar
                join p in db.Pessoas on cp.RecebedorId equals p.Id into pj
                from p in pj.DefaultIfEmpty()
                where cp.FamiliaId == u.FamiliaId
                   && cp.DataVencimento == dataAlvo
                   && cp.StatusContaId == StatusConta.PendenteId
                select new
                {
                    cp.Descricao,
                    cp.ValorLiquido,
                    RecebedorNome = p != null ? p.Nome : null
                })
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            if (contas.Count == 0) continue;

            var total = contas.Sum(c => c.ValorLiquido);
            var diasTexto = u.DiasAntecedenciaVencimento == 1 ? "amanhã" : $"em {u.DiasAntecedenciaVencimento} dias";

            var sb = new StringBuilder();
            sb.AppendLine($"🔔 *Lembrete de vencimento* ({dataAlvo:dd/MM/yyyy} — {diasTexto})");
            sb.AppendLine();

            foreach (var c in contas)
            {
                var recebedor = c.RecebedorNome is not null ? $" → {c.RecebedorNome}" : string.Empty;
                sb.AppendLine($"• {c.Descricao}{recebedor}: R$ {c.ValorLiquido:N2}");
            }

            sb.AppendLine();
            sb.Append($"*Total: R$ {total:N2}*");
            if (contas.Count > 1)
                sb.Append($" ({contas.Count} contas)");

            logger.LogInformation("Alerta vencimento → {Telefone}: {Count} conta(s) em {Data}",
                u.Telefone, contas.Count, dataAlvo);

            await outbound.EnviarAsync(u.Telefone, sb.ToString(), cancellationToken);
            await RegistrarEnvioAsync(u.Telefone, AlertaWhatsappEnviado.TipoVencimento, chave, hoje, cancellationToken);
        }
    }

    // ─── Alertas de limite de categoria ─────────────────────────────────────

    private async Task ProcessarLimitesCategoriaAsync(DateOnly hoje, CancellationToken cancellationToken)
    {
        var usuariosAlerta = await (
            from wup in db.WhatsappUsuarios
            join cfg in db.WhatsappConfigAlertas on wup.UsuarioId equals cfg.UsuarioId
            where wup.Ativo && cfg.ReceberLimiteCategoria
            select new
            {
                wup.Telefone,
                wup.FamiliaId
            })
            .AsNoTracking()
            .Distinct()
            .ToListAsync(cancellationToken);

        if (usuariosAlerta.Count == 0) return;

        var competencia = hoje.ToString("yyyy-MM");
        var inicioMes = new DateOnly(hoje.Year, hoje.Month, 1);
        var fimMes = inicioMes.AddMonths(1).AddDays(-1);

        // Agrupa por família para não repetir consultas pesadas
        var familias = usuariosAlerta.GroupBy(u => u.FamiliaId);

        foreach (var grupo in familias)
        {
            var familiaId = grupo.Key;

            var metas = await (
                from m in db.MetasOrcamento
                join cg in db.ContasGerenciais on m.ContaGerencialId equals cg.Id
                where m.FamiliaId == familiaId && m.Competencia == competencia
                select new
                {
                    m.ContaGerencialId,
                    m.ValorMeta,
                    CategoriaNome = cg.Descricao
                })
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            if (metas.Count == 0) continue;

            // Calcula o realizado por categoria via RateioContaGerencial
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
                .ToListAsync(cancellationToken);

            var realizadoMap = realizados.ToDictionary(x => x.ContaGerencialId, x => x.Total);

            foreach (var meta in metas)
            {
                var realizado = realizadoMap.GetValueOrDefault(meta.ContaGerencialId, 0m);
                var percentual = meta.ValorMeta > 0 ? realizado / meta.ValorMeta : 0m;

                if (percentual < 0.90m) continue;

                var chave = $"{meta.ContaGerencialId}:{competencia}";

                foreach (var usuario in grupo)
                {
                    if (await JaEnviouAsync(usuario.Telefone, AlertaWhatsappEnviado.TipoLimiteCategoria, chave, hoje, cancellationToken))
                        continue;

                    var emoji = percentual >= 1m ? "🚨" : "⚠️";
                    var pct = (percentual * 100m).ToString("N0");
                    var msg = $"{emoji} *Alerta de orçamento — {meta.CategoriaNome}*\n\n" +
                              $"Realizado: R$ {realizado:N2} de R$ {meta.ValorMeta:N2} ({pct}%)\n" +
                              $"Competência: {competencia}";

                    logger.LogInformation("Alerta limite categoria → {Telefone}: {Categoria} {Pct}%",
                        usuario.Telefone, meta.CategoriaNome, pct);

                    await outbound.EnviarAsync(usuario.Telefone, msg, cancellationToken);
                    await RegistrarEnvioAsync(usuario.Telefone, AlertaWhatsappEnviado.TipoLimiteCategoria, chave, hoje, cancellationToken);
                }
            }
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private Task<bool> JaEnviouAsync(
        string telefone, string tipo, string chave, DateOnly dataEnvio, CancellationToken cancellationToken) =>
        db.AlertasWhatsappEnviados.AnyAsync(
            a => a.Telefone == telefone
              && a.TipoAlerta == tipo
              && a.ChaveReferencia == chave
              && a.DataEnvio == dataEnvio,
            cancellationToken);

    private async Task RegistrarEnvioAsync(
        string telefone, string tipo, string chave, DateOnly dataEnvio, CancellationToken cancellationToken)
    {
        db.AlertasWhatsappEnviados.Add(
            AlertaWhatsappEnviado.Registrar(telefone, tipo, chave, dataEnvio));
        await db.SaveChangesAsync(cancellationToken);
    }
}
