using ControleFinanceiro.Application.Cadastros.ContasGerenciais;
using ControleFinanceiro.Contracts.Cadastros.ContasGerenciais;
using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Contracts.Errors;
using Microsoft.AspNetCore.Mvc;

namespace ControleFinanceiro.Api.Controllers;

[ApiController]
[Route("api/v1/contas-gerenciais")]
public sealed class ContasGerenciaisController(ContaGerencialAppService service) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ContaGerencialResumoResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ContaGerencialResumoResponse>>> Listar(
        [FromQuery] ContaGerencialListQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await service.ListarAsync(query, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ContaGerencialDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContaGerencialDetalheResponse>> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        var response = await service.ObterPorIdAsync(id, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ContaGerencialDetalheResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ContaGerencialDetalheResponse>> Criar(
        [FromBody] CriarContaGerencialRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.CriarAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id = response.Id }, response);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ContaGerencialDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContaGerencialDetalheResponse>> Atualizar(
        Guid id,
        [FromBody] AtualizarContaGerencialRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.AtualizarAsync(id, request, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }
}
