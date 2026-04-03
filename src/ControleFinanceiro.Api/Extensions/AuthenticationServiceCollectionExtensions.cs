using ControleFinanceiro.Api.Authentication;
using ControleFinanceiro.Api.Configuration;
using ControleFinanceiro.Contracts.Errors;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;

namespace ControleFinanceiro.Api.Extensions;

public static class AuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddApiFoundation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.ConfigureApiBehavior();
        services.AddApiAuthentication(configuration);
        return services;
    }

    private static IServiceCollection AddApiAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var authOptions = configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
        var useJwtBearer = string.Equals(authOptions.Mode, "JwtBearer", StringComparison.OrdinalIgnoreCase);
        var defaultScheme = useJwtBearer
            ? JwtBearerDefaults.AuthenticationScheme
            : DevelopmentAuthenticationHandler.SchemeName;

        var authenticationBuilder = services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = defaultScheme;
            options.DefaultChallengeScheme = defaultScheme;
        });

        if (useJwtBearer)
        {
            authenticationBuilder.AddJwtBearer(options =>
            {
                options.Authority = authOptions.Authority;
                options.Audience = authOptions.Audience;
                options.RequireHttpsMetadata = authOptions.RequireHttpsMetadata;
            });
        }
        else
        {
            authenticationBuilder.AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(
                DevelopmentAuthenticationHandler.SchemeName,
                _ => { });
        }

        return services;
    }

    private static IServiceCollection ConfigureApiBehavior(this IServiceCollection services)
    {
        services.PostConfigure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(entry => entry.Value?.Errors.Count > 0)
                    .ToDictionary(
                        entry => entry.Key,
                        entry => entry.Value!.Errors
                            .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                                ? "The supplied value is invalid."
                                : error.ErrorMessage)
                            .ToArray());

                var response = new ApiErrorResponse(
                    "VALIDATION_ERROR",
                    "One or more fields are invalid.",
                    errors,
                    context.HttpContext.TraceIdentifier);

                return new BadRequestObjectResult(response);
            };
        });

        return services;
    }
}
