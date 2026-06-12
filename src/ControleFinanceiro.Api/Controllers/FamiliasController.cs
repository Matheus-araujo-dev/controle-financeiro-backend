using ControleFinanceiro.Application.Identidade;
using ControleFinanceiro.Contracts.Errors;
using ControleFinanceiro.Contracts.Familias;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ControleFinanceiro.Api.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting("Standard")]
[Route("api/v1/familias")]
public sealed class FamiliasController(FamiliaAppService familiaAppService) : ApiControllerBase
{
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
}
