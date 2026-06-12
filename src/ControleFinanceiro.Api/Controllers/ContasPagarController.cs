using ControleFinanceiro.Application.Financeiro.ContasPagar;
using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Contracts.Errors;
using ControleFinanceiro.Contracts.Financeiro.Common;
using ControleFinanceiro.Contracts.Financeiro.ContasPagar;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ControleFinanceiro.Api.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting("Relaxed")]
[Route("api/v1/contas-pagar")]
public sealed class ContasPagarController(ContaPagarAppService service) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ContaPagarListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ContaPagarListResponse>> Listar(
        [FromQuery] ContaPagarListQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await service.ListarAsync(query, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ContaPagarDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContaPagarDetalheResponse>> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        var response = await service.ObterPorIdAsync(id, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ContaPagarDetalheResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ContaPagarDetalheResponse>> Criar(
        [FromBody] CriarContaPagarRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.CriarAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id = response.Id }, response);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ContaPagarDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContaPagarDetalheResponse>> Atualizar(
        Guid id,
        [FromBody] AtualizarContaPagarRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.AtualizarAsync(id, request, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }

    [HttpPost("{id:guid}/alterar-futuras")]
    [ProducesResponseType(typeof(ContaPagarDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContaPagarDetalheResponse>> AlterarFuturas(
        Guid id,
        [FromBody] AtualizarContaPagarRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.AlterarFuturasAsync(id, request, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }

    [HttpPost("{id:guid}/gerar-ocorrencias")]
    [ProducesResponseType(typeof(ContaPagarDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContaPagarDetalheResponse>> GerarOcorrencias(
        Guid id,
        [FromBody] GerarOcorrenciasRecorrenciaRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.GerarOcorrenciasAsync(id, request, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }

    [HttpPost("{id:guid}/pausar-recorrencia")]
    [ProducesResponseType(typeof(ContaPagarDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContaPagarDetalheResponse>> PausarRecorrencia(Guid id, CancellationToken cancellationToken)
    {
        var response = await service.PausarRecorrenciaAsync(id, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }

    [HttpPost("{id:guid}/encerrar-recorrencia")]
    [ProducesResponseType(typeof(ContaPagarDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContaPagarDetalheResponse>> EncerrarRecorrencia(
        Guid id,
        [FromBody] EncerrarRecorrenciaRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.EncerrarRecorrenciaAsync(id, request, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }

    [HttpPost("{id:guid}/liquidar")]
    [ProducesResponseType(typeof(ContaPagarDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContaPagarDetalheResponse>> Liquidar(
        Guid id,
        [FromBody] LiquidarContaPagarRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.LiquidarAsync(id, request, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }

    [HttpPost("{id:guid}/estornar")]
    [ProducesResponseType(typeof(ContaPagarDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContaPagarDetalheResponse>> Estornar(Guid id, CancellationToken cancellationToken)
    {
        var response = await service.EstornarAsync(id, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }

    [HttpPost("{id:guid}/cancelar")]
    [ProducesResponseType(typeof(ContaPagarDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContaPagarDetalheResponse>> Cancelar(Guid id, CancellationToken cancellationToken)
    {
        var response = await service.CancelarAsync(id, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }
}
