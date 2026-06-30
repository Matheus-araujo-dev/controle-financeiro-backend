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
public sealed class AgenteController(
    IFinanceAgentService agentService,
    FinanceInsightsService insightsService,
    FinanceCategorizacaoService categorizacaoService) : ApiControllerBase
{
    [HttpPost("perguntar")]
    [EnableRateLimiting(RateLimitPolicies.AiPolicy)]
    [ProducesResponseType(typeof(AgentePerguntarResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AgentePerguntarResponse>> Perguntar(
        [FromBody] AgentePerguntarRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Mensagem))
            return BadRequestResponse("Mensagem é obrigatória.", "mensagem");

        var result = await agentService.ProcessarAsync(
            new AgentRequest(request.Mensagem, request.ConversaId),
            cancellationToken);

        return Ok(new AgentePerguntarResponse(result.Resposta, result.ConversaId, result.TokensUsados));
    }

    [HttpPost("insights")]
    [EnableRateLimiting(RateLimitPolicies.AiPolicy)]
    [ProducesResponseType(typeof(AgenteInsightsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AgenteInsightsResponse>> Insights(
        [FromBody] AgenteInsightsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await insightsService.GerarInsightsAsync(request.MesReferencia, cancellationToken);
        return Ok(result);
    }

    [HttpPost("categorizar")]
    [EnableRateLimiting(RateLimitPolicies.AiPolicy)]
    [ProducesResponseType(typeof(AgenteCategorizarResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AgenteCategorizarResponse>> Categorizar(
        [FromBody] AgenteCategorizarRequest request,
        CancellationToken cancellationToken)
    {
        var result = await categorizacaoService.CategorizarAsync(request.Descricoes, cancellationToken);
        return Ok(new AgenteCategorizarResponse(result));
    }
}
