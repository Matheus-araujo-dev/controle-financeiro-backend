using ControleFinanceiro.Application.Cadastros.Pessoas;
using ControleFinanceiro.Contracts.Cadastros.Pessoas;
using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Contracts.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControleFinanceiro.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/pessoas")]
public sealed class PessoasController(PessoaAppService service) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<PessoaResumoResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<PessoaResumoResponse>>> Listar(
        [FromQuery] PessoaListQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await service.ListarAsync(query, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PessoaDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PessoaDetalheResponse>> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        var response = await service.ObterPorIdAsync(id, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }

    [HttpPost]
    [ProducesResponseType(typeof(PessoaDetalheResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PessoaDetalheResponse>> Criar(
        [FromBody] CriarPessoaRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.CriarAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id = response.Id }, response);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PessoaDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PessoaDetalheResponse>> Atualizar(
        Guid id,
        [FromBody] AtualizarPessoaRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.AtualizarAsync(id, request, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }

    [HttpPatch("{id:guid}/ativar")]
    [ProducesResponseType(typeof(PessoaDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PessoaDetalheResponse>> Ativar(Guid id, CancellationToken cancellationToken)
    {
        var response = await service.DefinirAtivacaoAsync(id, true, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }

    [HttpPatch("{id:guid}/inativar")]
    [ProducesResponseType(typeof(PessoaDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PessoaDetalheResponse>> Inativar(Guid id, CancellationToken cancellationToken)
    {
        var response = await service.DefinirAtivacaoAsync(id, false, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }
}
