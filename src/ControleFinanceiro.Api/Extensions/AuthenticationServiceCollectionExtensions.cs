using ControleFinanceiro.Api.Authentication;
using ControleFinanceiro.Api.Configuration;
using ControleFinanceiro.Contracts.Errors;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace ControleFinanceiro.Api.Extensions;

public static class AuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddApiFoundation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.ConfigureApiBehavior();
        services.AddApiCors(configuration);
        services.AddApiAuthentication(configuration);
        return services;
    }

    private static IServiceCollection AddApiCors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var corsOptions = configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();
        var allowedOrigins = corsOptions.AllowedOrigins
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (allowedOrigins.Length == 0)
        {
            allowedOrigins = new CorsOptions().AllowedOrigins;
        }

        services.AddCors(options =>
        {
            options.AddPolicy(CorsOptions.PolicyName, policy =>
            {
                policy.WithOrigins(allowedOrigins)
                    .WithHeaders("Authorization", "Content-Type", "X-Correlation-ID", "Accept", "Origin", "X-Debug-User")
                    .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS");
            });
        });

        return services;
    }

    private static IServiceCollection AddApiAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var authOptions = configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
        var useJwtBearer = string.Equals(authOptions.Mode, AuthOptions.JwtBearerMode, StringComparison.OrdinalIgnoreCase);
        var useSelfJwt = string.Equals(authOptions.Mode, AuthOptions.SelfJwtMode, StringComparison.OrdinalIgnoreCase);
        var defaultScheme = useJwtBearer || useSelfJwt
            ? JwtBearerDefaults.AuthenticationScheme
            : DevelopmentAuthenticationHandler.SchemeName;

        var authenticationBuilder = services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = defaultScheme;
            options.DefaultChallengeScheme = defaultScheme;
        });

        if (useSelfJwt)
        {
            if (string.IsNullOrWhiteSpace(authOptions.JwtSigningKey))
            {
                throw new InvalidOperationException(
                    "Auth:JwtSigningKey deve estar configurada quando o modo de autenticação é SelfJwt.");
            }

            authenticationBuilder.AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = authOptions.JwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = authOptions.JwtAudience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authOptions.JwtSigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });
        }
        else if (useJwtBearer)
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
