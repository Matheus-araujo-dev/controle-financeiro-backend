using ControleFinanceiro.Api.Configuration;
using ControleFinanceiro.Contracts.Security;
using ControleFinanceiro.SharedKernel.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ControleFinanceiro.Api.Controllers;

[ApiController]
[Route("api/v1/security")]
public sealed class SecurityController(
    ICurrentUser currentUser,
    IOptions<AuthOptions> authOptions) : ControllerBase
{
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(CurrentUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<CurrentUserResponse> Me()
    {
        return Ok(new CurrentUserResponse(
            currentUser.IsAuthenticated,
            currentUser.UserId,
            authOptions.Value.Mode));
    }
}
