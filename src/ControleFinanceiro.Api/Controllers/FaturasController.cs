using ControleFinanceiro.Application.Financeiro.Faturas;
using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Contracts.Errors;
using ControleFinanceiro.Contracts.Financeiro.Faturas;
using Microsoft.AspNetCore.Mvc;

namespace ControleFinanceiro.Api.Controllers;

[ApiController]
[Route("api/v1/faturas")]
public sealed class FaturasController(FaturaCartaoAppService service) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<FaturaResumoResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<FaturaResumoResponse>>> Listar(
        [FromQuery] FaturaListQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await service.ListarAsync(query, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(FaturaDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FaturaDetalheResponse>> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        var response = await service.ObterPorIdAsync(id, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }

    [HttpPost("{id:guid}/pagar")]
    [ProducesResponseType(typeof(FaturaDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FaturaDetalheResponse>> Pagar(
        Guid id,
        [FromBody] PagarFaturaRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.PagarAsync(id, request, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }
}
