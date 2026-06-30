using ControleFinanceiro.Application.Identidade;
using ControleFinanceiro.Contracts.Auth;
using ControleFinanceiro.Contracts.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ControleFinanceiro.Api.Controllers;

[ApiController]
[EnableRateLimiting("Strict")]
[Route("api/v1/auth")]
public sealed class AuthController(AuthAppService authAppService, IWebHostEnvironment env) : ControllerBase
{
    private const string RefreshTokenCookieName = "refreshToken";

    [AllowAnonymous]
    [HttpPost("google")]
    [ProducesResponseType(typeof(AuthTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthTokenResponse>> LoginComGoogle(
        [FromBody] GoogleLoginRequest request,
        CancellationToken cancellationToken)
    {
        var response = await authAppService.LoginComGoogleAsync(request.IdToken, cancellationToken);
        SetRefreshTokenCookie(response.RefreshToken);
        return Ok(response with { RefreshToken = string.Empty });
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthTokenResponse>> Refresh(CancellationToken cancellationToken)
    {
        var token = Request.Cookies[RefreshTokenCookieName] ?? string.Empty;
        var response = await authAppService.RefreshAsync(token, cancellationToken);
        SetRefreshTokenCookie(response.RefreshToken);
        return Ok(response with { RefreshToken = string.Empty });
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var token = Request.Cookies[RefreshTokenCookieName];
        await authAppService.LogoutAsync(token, cancellationToken);
        ClearRefreshTokenCookie();
        return NoContent();
    }

    private void SetRefreshTokenCookie(string token)
    {
        Response.Cookies.Append(RefreshTokenCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = env.IsProduction(),
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            Path = "/api/v1/auth"
        });
    }

    private void ClearRefreshTokenCookie()
    {
        Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = env.IsProduction(),
            SameSite = SameSiteMode.Strict,
            Path = "/api/v1/auth"
        });
    }
}
