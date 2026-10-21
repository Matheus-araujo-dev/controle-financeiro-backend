using ControleFinanceiro.Application.Financeiro.Transferencias;
using ControleFinanceiro.Contracts.Errors;
using ControleFinanceiro.Contracts.Financeiro.Transferencias;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ControleFinanceiro.Api.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting("Relaxed")]
[Route("api/v1/transferencias")]
public sealed class TransferenciasController(TransferenciaAppService service) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(TransferenciaListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<TransferenciaListResponse>> Listar(
        [FromQuery] TransferenciaListQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await service.ListarAsync(query, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TransferenciaResumoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TransferenciaResumoResponse>> ObterPorId(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await service.ObterPorIdAsync(id, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }

    [HttpPost]
    [ProducesResponseType(typeof(TransferenciaResumoResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TransferenciaResumoResponse>> Criar(
        [FromBody] CriarTransferenciaRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.CriarAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id = response.Id }, response);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(TransferenciaResumoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TransferenciaResumoResponse>> Cancelar(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await service.CancelarAsync(id, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }
}
