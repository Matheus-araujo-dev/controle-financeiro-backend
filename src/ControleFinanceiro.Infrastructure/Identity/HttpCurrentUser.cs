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
            var user = httpContextAccessor.HttpContext?.User;

            if (user?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            return user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user.FindFirstValue(ClaimTypes.Name)
                ?? user.Identity?.Name;
        }
    }
}
