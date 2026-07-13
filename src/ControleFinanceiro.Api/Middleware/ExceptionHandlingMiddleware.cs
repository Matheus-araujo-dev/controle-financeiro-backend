using ControleFinanceiro.Application.Common.Exceptions;
using ControleFinanceiro.Contracts.Errors;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace ControleFinanceiro.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    // Redige pares chave=valor sensíveis. O valor aceita qualquer caractere que não seja
    // separador (espaço, aspas, & ou ;), cobrindo e-mails, tokens JWT, chaves com pontos etc.
    private static readonly Regex SensitiveDataPattern = new(
        @"(cpf|cnpj|senha|password|pass|token|secret|apikey|api[_-]?key|key|authorization|bearer|chavepix|chave[_-]?pix|pix|-chave|credito|cartao|cvv|agencia|conta|limite|email|e-mail|refresh|access)[""']?\s*[:=]\s*[""']?([^\s""'&;]+)[""']?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Redige CPF/CNPJ soltos (com ou sem máscara) que apareçam sem uma chave associada.
    private static readonly Regex DocumentoPattern = new(
        @"\b\d{3}\.?\d{3}\.?\d{3}-?\d{2}\b|\b\d{2}\.?\d{3}\.?\d{3}/?\d{4}-?\d{2}\b",
        RegexOptions.Compiled);

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
        catch (FaturaIndisponivelException exception)
        {
            await WriteErrorAsync(
                context,
                StatusCodes.Status422UnprocessableEntity,
                "FATURA_INDISPONIVEL",
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
        catch (OperationCanceledException)
        {
            // Cliente desconectou antes da resposta — não registrar como erro.
            if (!context.Response.HasStarted)
                context.Response.StatusCode = 499;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Outra operação alterou o mesmo registro entre a leitura e a gravação
            // (ex.: liquidação concorrente da mesma conta). O cliente deve recarregar e repetir.
            await WriteErrorAsync(
                context,
                StatusCodes.Status409Conflict,
                "CONCURRENCY_CONFLICT",
                "O registro foi modificado por outra operação. Recarregue os dados e tente novamente.",
                new Dictionary<string, string[]>());
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
        
        var semParesSensiveis = SensitiveDataPattern.Replace(input, "$1=[REDACTED]");
        return DocumentoPattern.Replace(semParesSensiveis, "[REDACTED]");
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
