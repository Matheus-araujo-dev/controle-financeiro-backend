using ControleFinanceiro.Application.Cadastros.Cartoes;
using ControleFinanceiro.Contracts.Cadastros.Cartoes;
using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Contracts.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControleFinanceiro.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/cartoes")]
public sealed class CartoesController(CartaoAppService service) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CartaoResumoResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<CartaoResumoResponse>>> Listar(
        [FromQuery] CartaoListQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await service.ListarAsync(query, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CartaoDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CartaoDetalheResponse>> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        var response = await service.ObterPorIdAsync(id, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CartaoDetalheResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CartaoDetalheResponse>> Criar(
        [FromBody] CriarCartaoRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.CriarAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id = response.Id }, response);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CartaoDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CartaoDetalheResponse>> Atualizar(
        Guid id,
        [FromBody] AtualizarCartaoRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.AtualizarAsync(id, request, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }
}
