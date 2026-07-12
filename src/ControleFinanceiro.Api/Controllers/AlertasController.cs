using ControleFinanceiro.Api.Filters;
using ControleFinanceiro.Application.Common.Alertas;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Contracts.Alertas;
using ControleFinanceiro.Contracts.Errors;
using ControleFinanceiro.Domain.FinanceAI;
using ControleFinanceiro.SharedKernel.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Api.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting("Relaxed")]
[Route("api/v1/alertas")]
public sealed class AlertasController(
    IAppDbContext db,
    ICurrentUser currentUser,
    IPushAlertaService pushService) : ApiControllerBase
{
    // ── Configuração de notificações (email + push) ──────────────────────────

    [HttpGet("configuracao")]
    [ProducesResponseType(typeof(ConfiguracaoNotificacaoResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ConfiguracaoNotificacaoResponse>> ObterConfiguracao(
        CancellationToken cancellationToken)
    {
        var usuarioId = ResolverUsuarioId();
        if (usuarioId is null) return Unauthorized();

        var cfg = await db.ConfiguracoesNotificacao
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UsuarioId == usuarioId.Value, cancellationToken);

        if (cfg is null)
            return Ok(new ConfiguracaoNotificacaoResponse(false, null, true, 3, false, false, true, 1, false));

        return Ok(Projetar(cfg));
    }

    [HttpPut("configuracao")]
    [RequireFamiliaId]
    [ProducesResponseType(typeof(ConfiguracaoNotificacaoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ConfiguracaoNotificacaoResponse>> SalvarConfiguracao(
        [FromBody] SalvarConfiguracaoNotificacaoRequest request,
        CancellationToken cancellationToken)
    {
        var usuarioId = ResolverUsuarioId();
        if (usuarioId is null) return Unauthorized();

        var cfg = await db.ConfiguracoesNotificacao
            .FirstOrDefaultAsync(c => c.UsuarioId == usuarioId.Value, cancellationToken);

        if (cfg is null)
        {
            cfg = ConfiguracaoNotificacao.CriarPadrao(currentUser.FamiliaId!.Value, usuarioId.Value);
            db.ConfiguracoesNotificacao.Add(cfg);
        }

        cfg.Atualizar(
            request.EmailAtivo,
            request.EmailDestinatario,
            request.EmailVencimento,
            request.EmailDiasAntecedencia,
            request.EmailLimiteCategoria,
            request.PushAtivo,
            request.PushVencimento,
            request.PushDiasAntecedencia,
            request.PushLimiteCategoria);

        await db.SaveChangesAsync(cancellationToken);
        return Ok(Projetar(cfg));
    }

    // ── Web Push subscriptions ───────────────────────────────────────────────

    [HttpGet("push/vapid-key")]
    [ProducesResponseType(typeof(VapidPublicKeyResponse), StatusCodes.Status200OK)]
    public ActionResult<VapidPublicKeyResponse> ObterVapidPublicKey() =>
        Ok(new VapidPublicKeyResponse(pushService.GetVapidPublicKey()));

    [HttpGet("push/subscriptions")]
    [ProducesResponseType(typeof(List<PushSubscriptionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PushSubscriptionResponse>>> ListarSubscriptions(
        CancellationToken cancellationToken)
    {
        var usuarioId = ResolverUsuarioId();
        if (usuarioId is null) return Unauthorized();

        var subs = await db.PushSubscriptions
            .AsNoTracking()
            .Where(s => s.UsuarioId == usuarioId.Value && s.Ativo)
            .Select(s => new PushSubscriptionResponse(s.Id, s.Endpoint, s.Ativo))
            .ToListAsync(cancellationToken);

        return Ok(subs);
    }

    [HttpPost("push/subscriptions")]
    [RequireFamiliaId]
    [ProducesResponseType(typeof(PushSubscriptionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PushSubscriptionResponse>> RegistrarSubscription(
        [FromBody] RegistrarPushSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        var usuarioId = ResolverUsuarioId();
        if (usuarioId is null) return Unauthorized();

        // Idempotência: mesmo endpoint já registrado e ativo
        var existing = await db.PushSubscriptions
            .FirstOrDefaultAsync(s => s.UsuarioId == usuarioId.Value && s.Endpoint == request.Endpoint, cancellationToken);

        if (existing is not null)
            return Ok(new PushSubscriptionResponse(existing.Id, existing.Endpoint, existing.Ativo));

        var sub = PushSubscription.Registrar(
            currentUser.FamiliaId!.Value,
            usuarioId.Value,
            request.Endpoint,
            request.P256dh,
            request.Auth);

        db.PushSubscriptions.Add(sub);
        await db.SaveChangesAsync(cancellationToken);

        var response = new PushSubscriptionResponse(sub.Id, sub.Endpoint, sub.Ativo);
        return CreatedAtAction(nameof(ListarSubscriptions), response);
    }

    [HttpDelete("push/subscriptions/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoverSubscription(
        Guid id,
        CancellationToken cancellationToken)
    {
        var usuarioId = ResolverUsuarioId();
        if (usuarioId is null) return Unauthorized();

        var sub = await db.PushSubscriptions
            .FirstOrDefaultAsync(s => s.Id == id && s.UsuarioId == usuarioId.Value, cancellationToken);

        if (sub is null) return NotFoundResponse();

        sub.Desativar();
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ConfiguracaoNotificacaoResponse Projetar(ConfiguracaoNotificacao c) =>
        new(c.EmailAtivo, c.EmailDestinatario, c.EmailVencimento, c.EmailDiasAntecedencia, c.EmailLimiteCategoria,
            c.PushAtivo, c.PushVencimento, c.PushDiasAntecedencia, c.PushLimiteCategoria);

    private Guid? ResolverUsuarioId() =>
        Guid.TryParse(currentUser.UserId, out var id) ? id : null;
}
