using ControleFinanceiro.Contracts.Errors;
using Microsoft.AspNetCore.Mvc;

namespace ControleFinanceiro.Api.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    protected NotFoundObjectResult NotFoundResponse(string message = "Registro não encontrado.")
    {
        return NotFound(new ApiErrorResponse(
            "NOT_FOUND",
            message,
            new Dictionary<string, string[]>(),
            HttpContext.TraceIdentifier));
    }

    protected BadRequestObjectResult BadRequestResponse(string message, string field = "")
    {
        var errors = string.IsNullOrEmpty(field)
            ? new Dictionary<string, string[]>()
            : new Dictionary<string, string[]> { [field] = [message] };

        return BadRequest(new ApiErrorResponse(
            "BAD_REQUEST",
            message,
            errors,
            HttpContext.TraceIdentifier));
    }
}
