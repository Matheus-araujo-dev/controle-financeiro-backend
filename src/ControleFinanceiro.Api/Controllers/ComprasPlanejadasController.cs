using ControleFinanceiro.Application.PlanejamentoCompras;
using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Contracts.Errors;
using ControleFinanceiro.Contracts.PlanejamentoCompras;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControleFinanceiro.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/compras-planejadas")]
public sealed class ComprasPlanejadasController(PlanejamentoCompraAppService service) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(CompraPlanejadaListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CompraPlanejadaListResponse>> Listar(
        [FromQuery] CompraPlanejadaListQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await service.ListarAsync(query, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CompraPlanejadaDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompraPlanejadaDetalheResponse>> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        var response = await service.ObterPorIdAsync(id, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CompraPlanejadaDetalheResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CompraPlanejadaDetalheResponse>> Criar(
        [FromBody] CriarCompraPlanejadaRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.CriarAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id = response.Id }, response);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CompraPlanejadaDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompraPlanejadaDetalheResponse>> Atualizar(
        Guid id,
        [FromBody] AtualizarCompraPlanejadaRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.AtualizarAsync(id, request, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }

    [HttpPost("{id:guid}/realizar")]
    [ProducesResponseType(typeof(CompraPlanejadaDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompraPlanejadaDetalheResponse>> Realizar(
        Guid id,
        [FromBody] RealizarCompraPlanejadaRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.RealizarAsync(id, request, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }
}
