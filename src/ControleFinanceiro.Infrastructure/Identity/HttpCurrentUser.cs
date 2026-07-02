using System.Security.Claims;
using ControleFinanceiro.SharedKernel.Abstractions;
using Microsoft.AspNetCore.Http;

namespace ControleFinanceiro.Infrastructure.Identity;

public sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public bool IsAuthenticated =>
        httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    public string? UserId
    {
        get
        {
            var user = AuthenticatedUser();

            return user?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user?.FindFirstValue("sub")
                ?? user?.FindFirstValue(ClaimTypes.Name)
                ?? user?.Identity?.Name;
        }
    }

    public Guid? WorkspaceId
    {
        get
        {
            var claim = AuthenticatedUser()?.FindFirstValue(JwtTokenService.WorkspaceClaim)
                ?? AuthenticatedUser()?.FindFirstValue(JwtTokenService.FamiliaClaim);

            return Guid.TryParse(claim, out var workspaceId) ? workspaceId : null;
        }
    }

    public Guid? FamiliaId => WorkspaceId;

    public string? Papel => AuthenticatedUser()?.FindFirstValue(JwtTokenService.PapelClaim);

    private ClaimsPrincipal? AuthenticatedUser()
    {
        var user = httpContextAccessor.HttpContext?.User;
        return user?.Identity?.IsAuthenticated == true ? user : null;
    }
}
