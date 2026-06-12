using ControleFinanceiro.Application.Financeiro.Movimentacoes;
using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Contracts.Errors;
using ControleFinanceiro.Contracts.Financeiro.Movimentacoes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControleFinanceiro.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/movimentacoes")]
public sealed class MovimentacoesController(MovimentacaoFinanceiraAppService service) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(MovimentacaoListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<MovimentacaoListResponse>> Listar(
        [FromQuery] MovimentacaoListQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await service.ListarAsync(query, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(MovimentacaoDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MovimentacaoDetalheResponse>> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        var response = await service.ObterPorIdAsync(id, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }
}
