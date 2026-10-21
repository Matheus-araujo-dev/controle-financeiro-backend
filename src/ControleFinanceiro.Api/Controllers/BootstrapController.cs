using ControleFinanceiro.Application.Bootstrap;
using ControleFinanceiro.Contracts.Bootstrap;
using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Contracts.Filters;
using ControleFinanceiro.SharedKernel.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControleFinanceiro.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/bootstrap")]
public sealed class BootstrapController(
    IBootstrapCatalogService bootstrapCatalogService,
    IClock clock) : ControllerBase
{
    [HttpGet("status")]
    [ProducesResponseType(typeof(BootstrapStatusResponse), StatusCodes.Status200OK)]
    public ActionResult<BootstrapStatusResponse> GetStatus()
    {
        // Não expõe o modo de autenticação: endpoint público não deve facilitar reconhecimento.
        return Ok(new BootstrapStatusResponse(
            "Controle Financeiro API",
            "v1",
            HttpContext.TraceIdentifier,
            clock.UtcNow));
    }

    [HttpGet("modules")]
    [ProducesResponseType(typeof(PagedResult<BootstrapModuleItemResponse>), StatusCodes.Status200OK)]
    public ActionResult<PagedResult<BootstrapModuleItemResponse>> GetModules([FromQuery] ListQueryRequest query)
    {
        var result = bootstrapCatalogService.ListModules(query);
        return Ok(result);
    }

    [HttpPost("echo")]
    [ProducesResponseType(typeof(BootstrapEchoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<BootstrapEchoResponse> Echo([FromBody] BootstrapEchoRequest request)
    {
        var normalizedName = request.Name.Trim();
        return Ok(new BootstrapEchoResponse(normalizedName, normalizedName.Length));
    }
}
