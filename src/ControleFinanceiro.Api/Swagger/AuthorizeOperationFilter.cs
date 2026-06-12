using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ControleFinanceiro.Api.Swagger;

public sealed class AuthorizeOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var hasAllowAnonymous = context.MethodInfo.DeclaringType?.GetCustomAttributes(true)
                .OfType<AllowAnonymousAttribute>()
                .Any() == true
            || context.MethodInfo.GetCustomAttributes(true)
                .OfType<AllowAnonymousAttribute>()
                .Any();

        if (hasAllowAnonymous)
        {
            return;
        }

        var hasAuthorize = context.MethodInfo.DeclaringType?.GetCustomAttributes(true)
                .OfType<AuthorizeAttribute>()
                .Any() == true
            || context.MethodInfo.GetCustomAttributes(true)
                .OfType<AuthorizeAttribute>()
                .Any();

        if (!hasAuthorize)
        {
            return;
        }

        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Unauthorized" });
        operation.Security ??= new List<OpenApiSecurityRequirement>();
        operation.Security.Add(CreateRequirement("Bearer"));
        operation.Security.Add(CreateRequirement("DebugUser"));
    }

    private static OpenApiSecurityRequirement CreateRequirement(string schemeId)
    {
        return new OpenApiSecurityRequirement
        {
            [
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Id = schemeId,
                        Type = ReferenceType.SecurityScheme
                    }
                }
            ] = Array.Empty<string>()
        };
    }
}
