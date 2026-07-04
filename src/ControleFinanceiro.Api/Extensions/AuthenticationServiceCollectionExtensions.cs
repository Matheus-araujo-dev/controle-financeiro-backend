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
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.ConfigureApiBehavior();
        services.AddApiCors(configuration);
        services.AddApiAuthentication(configuration, environment);
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
                    .WithHeaders("Authorization", "Content-Type", "X-Correlation-ID", "Accept", "Origin")
                    .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
                    .AllowCredentials();
            });
        });

        return services;
    }

    private static IServiceCollection AddApiAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var authOptions = configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
        var useJwtBearer = string.Equals(authOptions.Mode, AuthOptions.JwtBearerMode, StringComparison.OrdinalIgnoreCase);
        var useSelfJwt = string.Equals(authOptions.Mode, AuthOptions.SelfJwtMode, StringComparison.OrdinalIgnoreCase);
        var useDevelopment = string.Equals(authOptions.Mode, AuthOptions.DevelopmentMode, StringComparison.OrdinalIgnoreCase);

        // Fail-closed: qualquer valor de Auth:Mode não reconhecido é rejeitado no startup.
        // Sem isso, um typo ou uma env var mal configurada (ex.: "disable") cairia
        // silenciosamente no handler de desenvolvimento, que autentica qualquer requisição
        // com o header X-Debug-User e concede papel Administrador.
        if (!useJwtBearer && !useSelfJwt && !useDevelopment)
        {
            throw new InvalidOperationException(
                $"Auth:Mode inválido: '{authOptions.Mode}'. Valores permitidos: " +
                $"'{AuthOptions.JwtBearerMode}', '{AuthOptions.SelfJwtMode}', '{AuthOptions.DevelopmentMode}'.");
        }

        // O modo Development (bypass via X-Debug-User) nunca pode ser habilitado fora de
        // um ambiente de desenvolvimento. "Testing" é permitido para os testes de integração.
        if (useDevelopment && !environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
        {
            throw new InvalidOperationException(
                $"Auth:Mode='{AuthOptions.DevelopmentMode}' é proibido no ambiente '{environment.EnvironmentName}'. " +
                "Use 'SelfJwt' ou 'JwtBearer' em produção.");
        }

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
            if (string.IsNullOrWhiteSpace(authOptions.JwtSigningKey)
                || authOptions.JwtSigningKey.Contains("${", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Auth:JwtSigningKey deve estar configurada quando o modo de autenticação é SelfJwt.");
            }

            if (Encoding.UTF8.GetByteCount(authOptions.JwtSigningKey) < 32)
            {
                throw new InvalidOperationException(
                    "Auth:JwtSigningKey deve ter pelo menos 32 bytes para assinar tokens JWT.");
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
