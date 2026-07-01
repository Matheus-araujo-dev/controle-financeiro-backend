using ControleFinanceiro.Application.Identidade;
using ControleFinanceiro.Contracts.Auth;
using ControleFinanceiro.Contracts.Errors;
using ControleFinanceiro.Contracts.Familias;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ControleFinanceiro.Api.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting("Standard")]
[Route("api/v1/familias")]
public sealed class FamiliasController(FamiliaAppService familiaAppService, IWebHostEnvironment env) : ApiControllerBase
{
    private const string RefreshTokenCookieName = "refreshToken";

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ParticipacaoFamiliaResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ParticipacaoFamiliaResponse>>> ListarMinhasFamilias(CancellationToken cancellationToken)
    {
        var response = await familiaAppService.ListarMinhasFamiliasAsync(cancellationToken);
        return Ok(response);
    }

    [HttpGet("minha")]
    [ProducesResponseType(typeof(FamiliaDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FamiliaDetalheResponse>> ObterMinhaFamilia(CancellationToken cancellationToken)
    {
        var response = await familiaAppService.ObterMinhaFamiliaAsync(cancellationToken);
        return response is null ? NotFoundResponse("Família não encontrada.") : Ok(response);
    }

    [HttpPut("minha")]
    [ProducesResponseType(typeof(FamiliaDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FamiliaDetalheResponse>> Renomear(
        [FromBody] RenomearFamiliaRequest request,
        CancellationToken cancellationToken)
    {
        var response = await familiaAppService.RenomearAsync(request.Nome, cancellationToken);
        return response is null ? NotFoundResponse("Família não encontrada.") : Ok(response);
    }

    [HttpPost("{id:guid}/selecionar")]
    [ProducesResponseType(typeof(SelecionarFamiliaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SelecionarFamiliaResponse>> SelecionarFamiliaAtiva(Guid id, CancellationToken cancellationToken)
    {
        var response = await familiaAppService.SelecionarFamiliaAtivaAsync(id, cancellationToken);
        if (response is null)
        {
            return NotFoundResponse("Participação não encontrada.");
        }

        SetRefreshTokenCookie(response.RefreshToken);
        return Ok(new SelecionarFamiliaResponse(response with { RefreshToken = string.Empty }));
    }

    [HttpPost("convites")]
    [ProducesResponseType(typeof(ConviteCriadoResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ConviteCriadoResponse>> CriarConvite(
        [FromBody] CriarConviteFamiliaRequest request,
        CancellationToken cancellationToken)
    {
        var response = await familiaAppService.CriarConviteAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpDelete("convites/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevogarConvite(Guid id, CancellationToken cancellationToken)
    {
        var revogado = await familiaAppService.RevogarConviteAsync(id, cancellationToken);
        return revogado ? NoContent() : NotFoundResponse("Convite não encontrado.");
    }

    [HttpGet("convites/{token}")]
    [ProducesResponseType(typeof(ConviteDetalhePublicoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConviteDetalhePublicoResponse>> ObterConvite(
        string token,
        CancellationToken cancellationToken)
    {
        var response = await familiaAppService.ObterConvitePorTokenAsync(token, cancellationToken);
        return response is null ? NotFoundResponse("Convite não encontrado.") : Ok(response);
    }

    [HttpPost("convites/{token}/aceitar")]
    [ProducesResponseType(typeof(FamiliaDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FamiliaDetalheResponse>> AceitarConvite(
        string token,
        CancellationToken cancellationToken)
    {
        var response = await familiaAppService.AceitarConviteAsync(token, cancellationToken);
        return response is null
            ? NotFoundResponse("Convite inválido ou expirado.")
            : Ok(response);
    }

    [HttpPut("membros/{id:guid}/papel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AlterarPapelMembro(
        Guid id,
        [FromBody] AlterarPapelMembroRequest request,
        CancellationToken cancellationToken)
    {
        var alterado = await familiaAppService.AlterarPapelMembroAsync(id, request.Papel, cancellationToken);
        return alterado ? NoContent() : NotFoundResponse("Membro não encontrado.");
    }

    [HttpDelete("membros/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoverMembro(Guid id, CancellationToken cancellationToken)
    {
        var removido = await familiaAppService.RemoverMembroAsync(id, cancellationToken);
        return removido ? NoContent() : NotFoundResponse("Membro não encontrado.");
    }

    private void SetRefreshTokenCookie(string token)
    {
        Response.Cookies.Append(RefreshTokenCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = env.IsProduction(),
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            Path = "/api/v1/auth"
        });
    }
}
