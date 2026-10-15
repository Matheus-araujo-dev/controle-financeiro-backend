using ControleFinanceiro.Application.Financeiro.Fechamentos;
using ControleFinanceiro.Contracts.Errors;
using ControleFinanceiro.Contracts.Financeiro.Fechamentos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControleFinanceiro.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/fechamentos")]
public sealed class FechamentosController(FechamentoMensalAppService service) : ApiControllerBase
{
    [HttpGet("{competencia}")]
    [ProducesResponseType(typeof(FechamentoMensalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FechamentoMensalResponse>> Obter(string competencia, CancellationToken cancellationToken) =>
        Ok(await service.ObterAsync(competencia, cancellationToken));

    [HttpPost("{competencia}/fechar")]
    [ProducesResponseType(typeof(FechamentoMensalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FechamentoMensalResponse>> Fechar(string competencia, CancellationToken cancellationToken) =>
        Ok(await service.FecharAsync(competencia, cancellationToken));

    [HttpPost("{competencia}/reabrir")]
    [ProducesResponseType(typeof(FechamentoMensalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FechamentoMensalResponse>> Reabrir(string competencia, ReabrirFechamentoMensalRequest request, CancellationToken cancellationToken) =>
        Ok(await service.ReabrirAsync(competencia, request.Justificativa, cancellationToken));
}
