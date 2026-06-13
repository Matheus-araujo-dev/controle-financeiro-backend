using ControleFinanceiro.Api.Configuration;
using ControleFinanceiro.Application.FinanceAI;
using ControleFinanceiro.Contracts.Agente;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ControleFinanceiro.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/agente")]
public sealed class AgenteController(IFinanceAgentService agentService) : ApiControllerBase
{
    [HttpPost("perguntar")]
    [EnableRateLimiting(RateLimitPolicies.AiPolicy)]
    [ProducesResponseType(typeof(AgentePerguntarResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AgentePerguntarResponse>> Perguntar(
        [FromBody] AgentePerquntarRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Mensagem))
            return BadRequest("Mensagem é obrigatória.");

        var result = await agentService.ProcessarAsync(
            new AgentRequest(request.Mensagem, request.ConversaId),
            cancellationToken);

        return Ok(new AgentePerguntarResponse(result.Resposta, result.ConversaId, result.TokensUsados));
    }
}
