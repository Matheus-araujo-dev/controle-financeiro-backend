using ControleFinanceiro.Application.Dashboard;
using ControleFinanceiro.Contracts.Dashboard;
using Microsoft.AspNetCore.Mvc;

namespace ControleFinanceiro.Api.Controllers;

[ApiController]
[Route("api/v1/dashboard")]
public sealed class DashboardController(DashboardAppService service) : ApiControllerBase
{
    [HttpGet("resumo")]
    [ProducesResponseType(typeof(DashboardResumoResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardResumoResponse>> ObterResumo(
        [FromQuery] DashboardResumoQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await service.ObterResumoAsync(query, cancellationToken));
    }

    [HttpGet("fluxo-caixa")]
    [ProducesResponseType(typeof(DashboardFluxoCaixaResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardFluxoCaixaResponse>> ObterFluxoCaixa(
        [FromQuery] DashboardFluxoCaixaQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await service.ObterFluxoCaixaAsync(query, cancellationToken));
    }
}
