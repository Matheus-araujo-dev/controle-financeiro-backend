using ControleFinanceiro.Application.Financeiro.ContasReceber;
using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Contracts.Errors;
using ControleFinanceiro.Contracts.Financeiro.Common;
using ControleFinanceiro.Contracts.Financeiro.ContasReceber;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ControleFinanceiro.Api.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting("Relaxed")]
[Route("api/v1/contas-receber")]
public sealed class ContasReceberController(ContaReceberAppService service) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ContaReceberListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ContaReceberListResponse>> Listar(
        [FromQuery] ContaReceberListQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await service.ListarAsync(query, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ContaReceberDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContaReceberDetalheResponse>> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        var response = await service.ObterPorIdAsync(id, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ContaReceberDetalheResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ContaReceberDetalheResponse>> Criar(
        [FromBody] CriarContaReceberRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.CriarAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id = response.Id }, response);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ContaReceberDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContaReceberDetalheResponse>> Atualizar(
        Guid id,
        [FromBody] AtualizarContaReceberRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.AtualizarAsync(id, request, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }

    [HttpPost("{id:guid}/alterar-futuras")]
    [ProducesResponseType(typeof(ContaReceberDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContaReceberDetalheResponse>> AlterarFuturas(
        Guid id,
        [FromBody] AtualizarContaReceberRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.AlterarFuturasAsync(id, request, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }

    [HttpPost("{id:guid}/gerar-ocorrencias")]
    [ProducesResponseType(typeof(ContaReceberDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContaReceberDetalheResponse>> GerarOcorrencias(
        Guid id,
        [FromBody] GerarOcorrenciasRecorrenciaRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.GerarOcorrenciasAsync(id, request, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }

    [HttpPost("{id:guid}/pausar-recorrencia")]
    [ProducesResponseType(typeof(ContaReceberDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContaReceberDetalheResponse>> PausarRecorrencia(Guid id, CancellationToken cancellationToken)
    {
        var response = await service.PausarRecorrenciaAsync(id, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }

    [HttpPost("{id:guid}/encerrar-recorrencia")]
    [ProducesResponseType(typeof(ContaReceberDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContaReceberDetalheResponse>> EncerrarRecorrencia(
        Guid id,
        [FromBody] EncerrarRecorrenciaRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.EncerrarRecorrenciaAsync(id, request, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }

    [HttpPost("{id:guid}/liquidar")]
    [ProducesResponseType(typeof(ContaReceberDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContaReceberDetalheResponse>> Liquidar(
        Guid id,
        [FromBody] LiquidarContaReceberRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.LiquidarAsync(id, request, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }

    [HttpPost("{id:guid}/estornar")]
    [ProducesResponseType(typeof(ContaReceberDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContaReceberDetalheResponse>> Estornar(Guid id, CancellationToken cancellationToken)
    {
        var response = await service.EstornarAsync(id, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }

    [HttpPost("{id:guid}/cancelar")]
    [ProducesResponseType(typeof(ContaReceberDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContaReceberDetalheResponse>> Cancelar(Guid id, CancellationToken cancellationToken)
    {
        var response = await service.CancelarAsync(id, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }
}
