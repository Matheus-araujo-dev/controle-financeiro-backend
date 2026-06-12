using ControleFinanceiro.Application.Common.Exceptions;
using ControleFinanceiro.Contracts.Errors;
using System.Text.RegularExpressions;

namespace ControleFinanceiro.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly Regex SensitiveDataPattern = new(
        @"(cpf|cnpj|senha|password|token|secret|key|chavepix|-chave|credito|limite)[""']?\s*[:=]\s*[""']?([a-zA-Z0-9@#$%^&*]+)[""']?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (AuthenticationFailedException exception)
        {
            await WriteErrorAsync(
                context,
                StatusCodes.Status401Unauthorized,
                "AUTH_FAILED",
                exception.Message,
                new Dictionary<string, string[]>());
        }
        catch (ApplicationValidationException exception)
        {
            await WriteErrorAsync(
                context,
                StatusCodes.Status400BadRequest,
                "VALIDATION_ERROR",
                exception.Message,
                exception.Errors);
        }
        catch (Exception exception)
        {
            var sanitizedPath = SanitizeLog(context.Request.Path.ToString());
            logger.LogError(exception, "Unexpected request failure for {Path}", sanitizedPath);

            await WriteErrorAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "UNEXPECTED_ERROR",
                "An unexpected error occurred.",
                new Dictionary<string, string[]>());
        }
    }

    private static string SanitizeLog(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;
        
        return SensitiveDataPattern.Replace(input, "$1=[REDACTED]");
    }

    private static Task WriteErrorAsync(
        HttpContext context,
        int statusCode,
        string code,
        string message,
        IReadOnlyDictionary<string, string[]> errors)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var response = new ApiErrorResponse(
            code,
            message,
            errors,
            context.TraceIdentifier);

        return context.Response.WriteAsJsonAsync(response);
    }
}
