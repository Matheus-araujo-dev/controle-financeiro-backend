using ControleFinanceiro.Contracts.Errors;
using ControleFinanceiro.SharedKernel.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ControleFinanceiro.Api.Filters;

/// <summary>
/// Garante que a requisição possui um FamiliaId válido no token.
/// Substitui a verificação manual "if (familiaId is null) return Unauthorized()" nos controllers.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class RequireFamiliaIdAttribute : Attribute, IFilterFactory
{
    public bool IsReusable => false;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider) =>
        new RequireFamiliaIdFilter(serviceProvider.GetRequiredService<ICurrentUser>());
}

internal sealed class RequireFamiliaIdFilter(ICurrentUser currentUser) : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (currentUser.FamiliaId is null)
        {
            context.Result = new UnauthorizedObjectResult(new ApiErrorResponse(
                "FAMILIA_ID_REQUIRED",
                "FamiliaId não encontrado no token. Faça login novamente.",
                new Dictionary<string, string[]>(),
                context.HttpContext.TraceIdentifier));
        }
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
