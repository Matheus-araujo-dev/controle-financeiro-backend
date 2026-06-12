using ControleFinanceiro.Application.Cadastros.FormasPagamento;
using ControleFinanceiro.Contracts.Cadastros.FormasPagamento;
using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Contracts.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControleFinanceiro.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/formas-pagamento")]
public sealed class FormasPagamentoController(FormaPagamentoAppService service) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<FormaPagamentoResumoResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<FormaPagamentoResumoResponse>>> Listar(
        [FromQuery] FormaPagamentoListQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await service.ListarAsync(query, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(FormaPagamentoDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FormaPagamentoDetalheResponse>> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        var response = await service.ObterPorIdAsync(id, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }

    [HttpPost]
    [ProducesResponseType(typeof(FormaPagamentoDetalheResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FormaPagamentoDetalheResponse>> Criar(
        [FromBody] CriarFormaPagamentoRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.CriarAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id = response.Id }, response);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(FormaPagamentoDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FormaPagamentoDetalheResponse>> Atualizar(
        Guid id,
        [FromBody] AtualizarFormaPagamentoRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.AtualizarAsync(id, request, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }
}
