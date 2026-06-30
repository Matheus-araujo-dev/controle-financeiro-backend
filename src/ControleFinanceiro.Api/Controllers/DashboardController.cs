using ControleFinanceiro.Application.Dashboard;
using ControleFinanceiro.Application.Dashboard.Queries;
using ControleFinanceiro.Api.Configuration;
using ControleFinanceiro.Contracts.Dashboard;
using ControleFinanceiro.Contracts.Errors;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControleFinanceiro.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/dashboard")]
public sealed class DashboardController(DashboardAppService service, ISender mediator) : ApiControllerBase
{
    [HttpGet("resumo")]
    [ProducesResponseType(typeof(DashboardResumoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DashboardResumoResponse>> ObterResumo(
        [FromQuery] DashboardResumoQueryRequest query,
        CancellationToken cancellationToken)
    {
        var err = ValidateMesReferencia(query.MesReferencia);
        if (err is not null) return err;
        return Ok(await mediator.Send(new GetDashboardResumoQuery(query), cancellationToken));
    }

    [HttpGet("fluxo-caixa")]
    [ProducesResponseType(typeof(DashboardFluxoCaixaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DashboardFluxoCaixaResponse>> ObterFluxoCaixa(
        [FromQuery] DashboardFluxoCaixaQueryRequest query,
        CancellationToken cancellationToken)
    {
        var err = ValidateMesReferencia(query.MesReferencia);
        if (err is not null) return err;
        return Ok(await service.ObterFluxoCaixaAsync(query, cancellationToken));
    }

    [HttpGet("contas-gerenciais/resumo")]
    [ProducesResponseType(typeof(DashboardContaGerencialResumoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DashboardContaGerencialResumoResponse>> ObterResumoContasGerenciais(
        [FromQuery] DashboardContaGerencialResumoQueryRequest query,
        CancellationToken cancellationToken)
    {
        var err = ValidateMesReferencia(query.MesReferencia);
        if (err is not null) return err;
        return Ok(await service.ObterContasGerenciaisResumoAsync(query, cancellationToken));
    }

    [HttpGet("contas-gerenciais/serie")]
    [ProducesResponseType(typeof(DashboardContaGerencialSerieResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DashboardContaGerencialSerieResponse>> ObterSerieContasGerenciais(
        [FromQuery] DashboardContaGerencialSerieQueryRequest query,
        CancellationToken cancellationToken)
    {
        var err = ValidateMesReferencia(query.MesReferencia);
        if (err is not null) return err;
        return Ok(await service.ObterContasGerenciaisSerieAsync(query, cancellationToken));
    }

    [HttpGet("contas-gerenciais/lancamentos")]
    [ProducesResponseType(typeof(DashboardContaGerencialLancamentosResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DashboardContaGerencialLancamentosResponse>> ObterLancamentosContaGerencial(
        [FromQuery] DashboardContaGerencialLancamentosQueryRequest query,
        CancellationToken cancellationToken)
    {
        var err = ValidateMesReferencia(query.MesReferencia);
        if (err is not null) return err;
        return Ok(await service.ObterContaGerencialLancamentosAsync(query, cancellationToken));
    }

    [HttpGet("responsaveis/resumo")]
    [ProducesResponseType(typeof(DashboardResponsavelResumoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DashboardResponsavelResumoResponse>> ObterResumoPorResponsavel(
        [FromQuery] DashboardResponsavelQueryRequest query,
        CancellationToken cancellationToken)
    {
        var err = ValidateMesReferencia(query.MesReferencia);
        if (err is not null) return err;
        return Ok(await service.ObterResumoPorResponsavelAsync(query, cancellationToken));
    }

    [HttpGet("central-previsao/resumo")]
    [ProducesResponseType(typeof(DashboardCentralPrevisaoResumoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DashboardCentralPrevisaoResumoResponse>> ObterResumoCentralPrevisao(
        [FromQuery] DashboardCentralPrevisaoQueryRequest query,
        CancellationToken cancellationToken)
    {
        var err = ValidateMesReferencia(query.MesReferencia);
        if (err is not null) return err;
        return Ok(await service.ObterCentralPrevisaoResumoAsync(query, cancellationToken));
    }

    [HttpGet("central-previsao/itens")]
    [ProducesResponseType(typeof(DashboardCentralPrevisaoItensResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DashboardCentralPrevisaoItensResponse>> ObterItensCentralPrevisao(
        [FromQuery] DashboardCentralPrevisaoItensQueryRequest query,
        CancellationToken cancellationToken)
    {
        var err = ValidateMesReferencia(query.MesReferencia);
        if (err is not null) return err;
        return Ok(await service.ObterCentralPrevisaoItensAsync(query, cancellationToken));
    }

    private BadRequestObjectResult? ValidateMesReferencia(string? mesReferencia)
    {
        if (mesReferencia is null) return null;
        return DateOnly.TryParseExact(mesReferencia, "yyyy-MM", out _)
            ? null
            : BadRequestResponse("Formato inválido. Use yyyy-MM (ex: 2025-06).", "mesReferencia");
    }
}
