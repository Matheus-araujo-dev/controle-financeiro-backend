using ControleFinanceiro.Application.Conciliacao;
using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Contracts.Conciliacao;
using ControleFinanceiro.Contracts.Errors;
using Microsoft.AspNetCore.Mvc;

namespace ControleFinanceiro.Api.Controllers;

[ApiController]
[Route("api/v1/conciliacao")]
public sealed class ConciliacaoController(ConciliacaoAppService service) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ConciliacaoItemResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ConciliacaoItemResponse>>> Listar(
        [FromQuery] ConciliacaoListQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await service.ListarAsync(query, cancellationToken));
    }

    [HttpPost("{itemImportadoWhatsappId:guid}/confirmar-vinculo")]
    [ProducesResponseType(typeof(ConciliacaoItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ConciliacaoItemResponse>> ConfirmarVinculo(
        Guid itemImportadoWhatsappId,
        [FromBody] ConfirmarVinculoConciliacaoRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.ConfirmarVinculoAsync(itemImportadoWhatsappId, request, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }
}
