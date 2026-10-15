using ControleFinanceiro.Application.Identidade;
using ControleFinanceiro.Contracts.Auth;
using ControleFinanceiro.Contracts.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ControleFinanceiro.Api.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting("Strict")]
[Route("api/v1/api-keys")]
public sealed class ApiKeysController(ApiKeyAppService service) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(ApiKeyCriadaResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiKeyCriadaResponse>> Criar(
        [FromBody] CriarApiKeyRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.CriarAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Listar), response);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ApiKeyResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ApiKeyResponse>>> Listar(CancellationToken cancellationToken)
    {
        return Ok(await service.ListarAsync(cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revogar(Guid id, CancellationToken cancellationToken)
    {
        var revogada = await service.RevogarAsync(id, cancellationToken);
        return revogada ? NoContent() : NotFoundResponse("API Key não encontrada.");
    }
}