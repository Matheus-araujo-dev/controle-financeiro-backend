using ControleFinanceiro.Application.ImportacoesWhatsapp;
using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Contracts.Errors;
using ControleFinanceiro.Contracts.ImportacoesWhatsapp;
using Microsoft.AspNetCore.Mvc;

namespace ControleFinanceiro.Api.Controllers;

[ApiController]
[Route("api/v1/importacoes-whatsapp")]
public sealed class ImportacoesWhatsappController(ImportacoesWhatsappAppService service) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ImportacaoWhatsappResumoResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ImportacaoWhatsappResumoResponse>>> Listar(
        [FromQuery] ImportacaoWhatsappListQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await service.ListarAsync(query, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ImportacaoWhatsappDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ImportacaoWhatsappDetalheResponse>> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        var response = await service.ObterPorIdAsync(id, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }

    [HttpPost("webhook")]
    [ProducesResponseType(typeof(ImportacaoWhatsappDetalheResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImportacaoWhatsappDetalheResponse>> ReceberWebhook(
        [FromBody] ReceberImportacaoWhatsappWebhookRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.ReceberWebhookAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id = response.Id }, response);
    }

    [HttpPost("{id:guid}/reprocessar")]
    [ProducesResponseType(typeof(ImportacaoWhatsappDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ImportacaoWhatsappDetalheResponse>> Reprocessar(Guid id, CancellationToken cancellationToken)
    {
        var response = await service.ReprocessarAsync(id, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }

    [HttpPost("itens/{id:guid}/confirmar")]
    [ProducesResponseType(typeof(ImportacaoWhatsappDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImportacaoWhatsappDetalheResponse>> Confirmar(
        Guid id,
        [FromBody] RevisarItemImportadoWhatsappRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.ConfirmarItemAsync(id, request, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }

    [HttpPost("itens/{id:guid}/rejeitar")]
    [ProducesResponseType(typeof(ImportacaoWhatsappDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImportacaoWhatsappDetalheResponse>> Rejeitar(
        Guid id,
        [FromBody] RevisarItemImportadoWhatsappRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.RejeitarItemAsync(id, request, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }
}
