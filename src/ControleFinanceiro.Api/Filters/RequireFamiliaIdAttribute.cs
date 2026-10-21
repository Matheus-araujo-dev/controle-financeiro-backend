using ControleFinanceiro.Contracts.Errors;
using ControleFinanceiro.SharedKernel.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ControleFinanceiro.Api.Filters;

/// <summary>
/// Garante que a requisicao possui um workspace ativo valido no token.
/// O atributo manteve o nome anterior por compatibilidade durante a transicao.
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
        if (currentUser.WorkspaceId is null)
        {
            context.Result = new UnauthorizedObjectResult(new ApiErrorResponse(
                "WORKSPACE_ID_REQUIRED",
                "WorkspaceId nao encontrado no token. Faca login novamente.",
                new Dictionary<string, string[]>(),
                context.HttpContext.TraceIdentifier));
        }
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
