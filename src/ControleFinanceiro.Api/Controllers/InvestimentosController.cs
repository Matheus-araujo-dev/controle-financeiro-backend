using ControleFinanceiro.Application.Financeiro.Investimentos;
using ControleFinanceiro.Contracts.Errors;
using ControleFinanceiro.Contracts.Financeiro.Investimentos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ControleFinanceiro.Api.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting("Relaxed")]
[Route("api/v1/investimentos")]
public sealed class InvestimentosController(InvestimentoAppService service) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(InvestimentoListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<InvestimentoListResponse>> Listar(
        [FromQuery] InvestimentoListQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await service.ListarAsync(query, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(InvestimentoResumoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvestimentoResumoResponse>> ObterPorId(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await service.ObterPorIdAsync(id, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }

    [HttpGet("indicadores-bcb")]
    [ProducesResponseType(typeof(IndicadoresBcbResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<IndicadoresBcbResponse>> ObterIndicadoresBcb(
        CancellationToken cancellationToken)
    {
        return Ok(await service.ObterIndicadoresBcbAsync(cancellationToken));
    }

    [HttpPost]
    [ProducesResponseType(typeof(InvestimentoResumoResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InvestimentoResumoResponse>> Criar(
        [FromBody] CriarInvestimentoRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.CriarAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id = response.Id }, response);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(InvestimentoResumoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InvestimentoResumoResponse>> Atualizar(
        Guid id,
        [FromBody] AtualizarInvestimentoRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.AtualizarAsync(id, request, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }

    [HttpPost("{id:guid}/atualizar-valor")]
    [ProducesResponseType(typeof(InvestimentoResumoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InvestimentoResumoResponse>> AtualizarValorAtual(
        Guid id,
        [FromBody] AtualizarValorAtualRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.AtualizarValorAtualAsync(id, request, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }

    [HttpPost("{id:guid}/encerrar")]
    [ProducesResponseType(typeof(InvestimentoResumoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InvestimentoResumoResponse>> Encerrar(
        Guid id,
        [FromBody] EncerrarInvestimentoRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.EncerrarAsync(id, request, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }
}
