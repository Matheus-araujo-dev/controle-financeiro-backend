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

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(RecorrenciaListItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecorrenciaListItemResponse>> Obter(Guid id, CancellationToken cancellationToken)
    {
        var response = await service.ObterAsync(id, cancellationToken);
        if (response is null) return NotFound();
        return Ok(response);
    }

    [HttpPost("{id:guid}/pausar")]
    [ProducesResponseType(typeof(RecorrenciaListItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecorrenciaListItemResponse>> Pausar(Guid id, CancellationToken cancellationToken)
    {
        var response = await service.PausarAsync(id, cancellationToken);
        return Ok(response);
    }

    [HttpPost("{id:guid}/retomar")]
    [ProducesResponseType(typeof(RecorrenciaListItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecorrenciaListItemResponse>> Retomar(Guid id, CancellationToken cancellationToken)
    {
        var response = await service.RetomarAsync(id, cancellationToken);
        return Ok(response);
    }

    [HttpPost("gerar-ocorrencias")]
    [ProducesResponseType(typeof(GerarOcorrenciasResultResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<GerarOcorrenciasResultResponse>> GerarOcorrencias(CancellationToken cancellationToken)
    {
        var resultado = await service.GerarOcorrenciasRecorrentesNoMesAsync(
            DateOnly.FromDateTime(DateTime.Now), cancellationToken);
        return Ok(resultado);
    }
}
