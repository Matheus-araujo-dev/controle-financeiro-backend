using ControleFinanceiro.Application.Bootstrap;
using ControleFinanceiro.Contracts.Bootstrap;
using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Contracts.Filters;
using ControleFinanceiro.SharedKernel.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ControleFinanceiro.Api.Configuration;

namespace ControleFinanceiro.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/bootstrap")]
public sealed class BootstrapController(
    IBootstrapCatalogService bootstrapCatalogService,
    IClock clock,
    IOptions<AuthOptions> authOptions) : ControllerBase
{
    [HttpGet("status")]
    [ProducesResponseType(typeof(BootstrapStatusResponse), StatusCodes.Status200OK)]
    public ActionResult<BootstrapStatusResponse> GetStatus()
    {
        return Ok(new BootstrapStatusResponse(
            "Controle Financeiro API",
            "v1",
            authOptions.Value.Mode,
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
