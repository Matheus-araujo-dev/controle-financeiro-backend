using ControleFinanceiro.Api.Filters;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Contracts.Agente;
using ControleFinanceiro.Contracts.Errors;
using ControleFinanceiro.Domain.FinanceAI;
using ControleFinanceiro.SharedKernel.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/perfil/whatsapp")]
public sealed class PerfilWhatsappController(IAppDbContext db, ICurrentUser currentUser) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(WhatsappPerfilResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<WhatsappPerfilResponse>> ObterStatus(CancellationToken cancellationToken)
    {
        var usuarioId = ResolverUsuarioId();
        if (usuarioId is null) return Unauthorized();

        var wup = await db.WhatsappUsuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.UsuarioId == usuarioId.Value, cancellationToken);

        if (wup is null)
            return Ok(new WhatsappPerfilResponse(null, false, null));

        return Ok(new WhatsappPerfilResponse(wup.Telefone, wup.Ativo, wup.VerificadoEm));
    }

    [HttpPut]
    [RequireFamiliaId]
    [ProducesResponseType(typeof(WhatsappPerfilResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<WhatsappPerfilResponse>> Registrar(
        [FromBody] WhatsappRegistrarRequest request,
        CancellationToken cancellationToken)
    {
        var usuarioId = ResolverUsuarioId();
        if (usuarioId is null) return Unauthorized();

        var telefone = WhatsappUsuario.NormalizarTelefone(request.Telefone);
        if (string.IsNullOrEmpty(telefone) || telefone.Length < 10)
            return BadRequestResponse("Número de telefone inválido.", "telefone");

        var wup = await db.WhatsappUsuarios
            .FirstOrDefaultAsync(w => w.UsuarioId == usuarioId.Value, cancellationToken);

        var agora = DateTimeOffset.UtcNow;
        if (wup is null)
        {
            wup = WhatsappUsuario.Criar(currentUser.FamiliaId!.Value, usuarioId.Value, telefone);
            wup.Verificar(agora);
            db.WhatsappUsuarios.Add(wup);
        }
        else
        {
            wup.AtualizarTelefone(telefone, agora);
        }
        await db.SaveChangesAsync(cancellationToken);

        return Ok(new WhatsappPerfilResponse(wup.Telefone, wup.Ativo, wup.VerificadoEm));
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Desativar(CancellationToken cancellationToken)
    {
        var usuarioId = ResolverUsuarioId();
        if (usuarioId is null) return Unauthorized();

        var wup = await db.WhatsappUsuarios
            .FirstOrDefaultAsync(w => w.UsuarioId == usuarioId.Value, cancellationToken);

        if (wup is not null)
        {
            wup.Desativar();
            await db.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    // ── Configuração de alertas ──────────────────────────────────────────────

    [HttpGet("alertas")]
    [ProducesResponseType(typeof(WhatsappAlertasResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<WhatsappAlertasResponse>> ObterAlertas(CancellationToken cancellationToken)
    {
        var usuarioId = ResolverUsuarioId();
        if (usuarioId is null) return Unauthorized();

        var cfg = await db.WhatsappConfigAlertas
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UsuarioId == usuarioId.Value, cancellationToken);

        if (cfg is null)
            return Ok(new WhatsappAlertasResponse(true, 3, false, false));

        return Ok(new WhatsappAlertasResponse(
            cfg.ReceberVencimento,
            cfg.DiasAntecedenciaVencimento,
            cfg.ReceberLimiteCategoria,
            cfg.ReceberLimiteResponsavel));
    }

    [HttpPut("alertas")]
    [RequireFamiliaId]
    [ProducesResponseType(typeof(WhatsappAlertasResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<WhatsappAlertasResponse>> AtualizarAlertas(
        [FromBody] WhatsappAlertasRequest request,
        CancellationToken cancellationToken)
    {
        var usuarioId = ResolverUsuarioId();
        if (usuarioId is null) return Unauthorized();

        var cfg = await db.WhatsappConfigAlertas
            .FirstOrDefaultAsync(c => c.UsuarioId == usuarioId.Value, cancellationToken);

        if (cfg is null)
        {
            cfg = WhatsappConfigAlerta.CriarPadrao(currentUser.FamiliaId!.Value, usuarioId.Value);
            db.WhatsappConfigAlertas.Add(cfg);
        }

        cfg.Atualizar(
            request.ReceberVencimento,
            request.DiasAntecedenciaVencimento,
            request.ReceberLimiteCategoria,
            request.ReceberLimiteResponsavel);

        await db.SaveChangesAsync(cancellationToken);

        return Ok(new WhatsappAlertasResponse(
            cfg.ReceberVencimento,
            cfg.DiasAntecedenciaVencimento,
            cfg.ReceberLimiteCategoria,
            cfg.ReceberLimiteResponsavel));
    }

    private Guid? ResolverUsuarioId() =>
        Guid.TryParse(currentUser.UserId, out var id) ? id : null;
}
