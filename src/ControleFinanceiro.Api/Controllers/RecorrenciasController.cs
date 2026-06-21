using ControleFinanceiro.Application.Financeiro.Recorrencias;
using ControleFinanceiro.Contracts.Financeiro.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControleFinanceiro.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/recorrencias")]
public sealed class RecorrenciasController(RecorrenciaAppService service) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(RecorrenciaListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RecorrenciaListResponse>> Listar(
        [FromQuery] RecorrenciaListQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await service.ListarAsync(query, cancellationToken));
    }
}
