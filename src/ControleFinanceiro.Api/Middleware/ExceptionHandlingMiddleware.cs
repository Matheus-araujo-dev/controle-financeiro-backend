using ControleFinanceiro.Application.Common.Exceptions;
using ControleFinanceiro.Contracts.Errors;

namespace ControleFinanceiro.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
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
            logger.LogError(exception, "Unexpected request failure for {Path}", context.Request.Path);

            await WriteErrorAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "UNEXPECTED_ERROR",
                "An unexpected error occurred.",
                new Dictionary<string, string[]>());
        }
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
