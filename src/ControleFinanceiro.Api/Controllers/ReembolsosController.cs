using ControleFinanceiro.Application.Financeiro.Reembolsos;
using ControleFinanceiro.Contracts.Errors;
using ControleFinanceiro.Contracts.Financeiro.Reembolsos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ControleFinanceiro.Api.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting("Relaxed")]
[Route("api/v1/reembolsos")]
public sealed class ReembolsosController(IReembolsoAppService service) : ApiControllerBase
{
    [HttpPost("contas-pagar")]
    [ProducesResponseType(typeof(CriarReembolsoContaPagarResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CriarReembolsoContaPagarResponse>> CriarReembolsoContaPagar(
        [FromBody] CriarReembolsoContaPagarRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.CriarReembolsoContaPagarAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }
}
