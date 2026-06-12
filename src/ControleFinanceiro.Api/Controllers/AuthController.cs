using ControleFinanceiro.Application.Identidade;
using ControleFinanceiro.Contracts.Auth;
using ControleFinanceiro.Contracts.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ControleFinanceiro.Api.Controllers;

[ApiController]
[EnableRateLimiting("Strict")]
[Route("api/v1/auth")]
public sealed class AuthController(AuthAppService authAppService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("google")]
    [ProducesResponseType(typeof(AuthTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthTokenResponse>> LoginComGoogle(
        [FromBody] GoogleLoginRequest request,
        CancellationToken cancellationToken)
    {
        var response = await authAppService.LoginComGoogleAsync(request.IdToken, cancellationToken);
        return Ok(response);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthTokenResponse>> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var response = await authAppService.RefreshAsync(request.RefreshToken, cancellationToken);
        return Ok(response);
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest request,
        CancellationToken cancellationToken)
    {
        await authAppService.LogoutAsync(request.RefreshToken, cancellationToken);
        return NoContent();
    }
}
