using ControleFinanceiro.Application.Financeiro.Planos;
using ControleFinanceiro.Contracts.Errors;
using ControleFinanceiro.Contracts.Financeiro.Planos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ControleFinanceiro.Api.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting("Relaxed")]
[Route("api/v1/planos")]
public sealed class PlanosController(PlanoAppService service) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PlanoListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PlanoListResponse>> Listar(
        [FromQuery] PlanoListQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await service.ListarAsync(query, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PlanoResumoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlanoResumoResponse>> ObterPorId(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await service.ObterPorIdAsync(id, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }

    [HttpPost]
    [ProducesResponseType(typeof(PlanoResumoResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PlanoResumoResponse>> Criar(
        [FromBody] CriarPlanoRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.CriarAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id = response.Id }, response);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PlanoResumoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PlanoResumoResponse>> Atualizar(
        Guid id,
        [FromBody] AtualizarPlanoRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.AtualizarAsync(id, request, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }

    [HttpPost("{id:guid}/adiantar-parcela")]
    [ProducesResponseType(typeof(PlanoResumoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PlanoResumoResponse>> AdiantarParcela(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await service.AdiantarParcelaAsync(id, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }

    [HttpPost("{id:guid}/retirar")]
    [ProducesResponseType(typeof(PlanoResumoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PlanoResumoResponse>> RetirarDinheiro(
        Guid id,
        [FromBody] RetirarDinheiroRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.RetirarDinheiroAsync(id, request, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(PlanoResumoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PlanoResumoResponse>> Cancelar(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await service.CancelarAsync(id, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }
}
