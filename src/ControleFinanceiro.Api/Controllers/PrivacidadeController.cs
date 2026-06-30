using ControleFinanceiro.Application.Privacidade;
using ControleFinanceiro.Contracts.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ControleFinanceiro.Api.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting("Strict")]
[Route("api/v1/privacidade")]
public sealed class PrivacidadeController(PrivacidadeAppService service) : ApiControllerBase
{
    /// <summary>
    /// Solicita o esquecimento dos dados pessoais do usuário autenticado (Art. 18, LGPD).
    /// Anonimiza PII e revoga sessões ativas. Dados financeiros são preservados por obrigação legal.
    /// </summary>
    [HttpDelete("dados")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SolicitarEsquecimento(CancellationToken cancellationToken)
    {
        await service.SolicitarEsquecimentoAsync(cancellationToken);
        return NoContent();
    }
}
