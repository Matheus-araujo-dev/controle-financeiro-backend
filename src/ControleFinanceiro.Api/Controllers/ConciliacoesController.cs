using ControleFinanceiro.Application.Conciliacao;
using ControleFinanceiro.Contracts.Conciliacao;
using ControleFinanceiro.Contracts.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControleFinanceiro.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/conciliacoes")]
public sealed class ConciliacoesController(ConciliacaoAppService service) : ApiControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [ProducesResponseType(typeof(ConciliacaoDetalheResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ConciliacaoDetalheResponse>> Iniciar(
        [FromForm] Guid contaBancariaId,
        IFormFile arquivo,
        CancellationToken cancellationToken)
    {
        await using var stream = arquivo.OpenReadStream();
        var response = await service.IniciarAsync(contaBancariaId, arquivo.FileName, stream, cancellationToken);
        return response is null ? NotFoundResponse() : Created(string.Empty, response);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<ConciliacaoResumoResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<ConciliacaoResumoResponse>>> Listar(CancellationToken cancellationToken)
    {
        return Ok(await service.ListarAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ConciliacaoDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConciliacaoDetalheResponse>> Obter(Guid id, CancellationToken cancellationToken)
    {
        var response = await service.ObterAsync(id, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }

    [HttpPatch("{conciliacaoId:guid}/itens/{itemId:guid}/conciliar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConciliarItem(
        Guid conciliacaoId, Guid itemId,
        [FromBody] ConciliarItemRequest request,
        CancellationToken cancellationToken)
    {
        return await service.ConciliarItemAsync(conciliacaoId, itemId, request, cancellationToken)
            ? NoContent() : NotFoundResponse();
    }

    [HttpPatch("{conciliacaoId:guid}/itens/{itemId:guid}/ignorar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> IgnorarItem(
        Guid conciliacaoId, Guid itemId,
        CancellationToken cancellationToken)
    {
        return await service.IgnorarItemAsync(conciliacaoId, itemId, cancellationToken)
            ? NoContent() : NotFoundResponse();
    }
}