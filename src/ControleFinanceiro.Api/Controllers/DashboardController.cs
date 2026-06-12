using ControleFinanceiro.Application.Dashboard;
using ControleFinanceiro.Api.Configuration;
using ControleFinanceiro.Contracts.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControleFinanceiro.Api.Controllers;

[ApiController]
[Authorize]
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

    [HttpGet("contas-gerenciais/resumo")]
    [ProducesResponseType(typeof(DashboardContaGerencialResumoResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardContaGerencialResumoResponse>> ObterResumoContasGerenciais(
        [FromQuery] DashboardContaGerencialResumoQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await service.ObterContasGerenciaisResumoAsync(query, cancellationToken));
    }

    [HttpGet("contas-gerenciais/serie")]
    [ProducesResponseType(typeof(DashboardContaGerencialSerieResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardContaGerencialSerieResponse>> ObterSerieContasGerenciais(
        [FromQuery] DashboardContaGerencialSerieQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await service.ObterContasGerenciaisSerieAsync(query, cancellationToken));
    }

    [HttpGet("contas-gerenciais/lancamentos")]
    [ProducesResponseType(typeof(DashboardContaGerencialLancamentosResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardContaGerencialLancamentosResponse>> ObterLancamentosContaGerencial(
        [FromQuery] DashboardContaGerencialLancamentosQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await service.ObterContaGerencialLancamentosAsync(query, cancellationToken));
    }

    [HttpGet("responsaveis/resumo")]
    [ProducesResponseType(typeof(DashboardResponsavelResumoResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardResponsavelResumoResponse>> ObterResumoPorResponsavel(
        [FromQuery] DashboardResponsavelQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await service.ObterResumoPorResponsavelAsync(query, cancellationToken));
    }

    [HttpGet("central-previsao/resumo")]
    [ProducesResponseType(typeof(DashboardCentralPrevisaoResumoResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardCentralPrevisaoResumoResponse>> ObterResumoCentralPrevisao(
        [FromQuery] DashboardCentralPrevisaoQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await service.ObterCentralPrevisaoResumoAsync(query, cancellationToken));
    }

    [HttpGet("central-previsao/itens")]
    [ProducesResponseType(typeof(DashboardCentralPrevisaoItensResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardCentralPrevisaoItensResponse>> ObterItensCentralPrevisao(
        [FromQuery] DashboardCentralPrevisaoItensQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await service.ObterCentralPrevisaoItensAsync(query, cancellationToken));
    }
}
