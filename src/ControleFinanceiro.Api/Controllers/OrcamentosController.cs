using ControleFinanceiro.Application.Financeiro.Orcamentos;
using ControleFinanceiro.Contracts.Errors;
using ControleFinanceiro.Contracts.Financeiro.Orcamentos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControleFinanceiro.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/orcamentos")]
public sealed class OrcamentosController(OrcamentoAppService service) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(OrcamentoCompetenciaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrcamentoCompetenciaResponse>> ObterPorCompetencia(
        [FromQuery] OrcamentoQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await service.ObterPorCompetenciaAsync(query, cancellationToken));
    }

    [HttpPut("metas")]
    [ProducesResponseType(typeof(MetaOrcamentoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MetaOrcamentoResponse>> UpsertMeta(
        [FromBody] UpsertMetaOrcamentoRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await service.UpsertMetaAsync(request, cancellationToken));
    }

    [HttpDelete("metas/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoverMeta(Guid id, CancellationToken cancellationToken)
    {
        var removida = await service.RemoverMetaAsync(id, cancellationToken);

        return removida ? NoContent() : NotFoundResponse("Meta de orçamento não encontrada.");
    }
}
