using ControleFinanceiro.Api.Filters;
using ControleFinanceiro.Application.Financeiro.Faturas;
using ControleFinanceiro.Application.Financeiro.Importacao;
using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Contracts.Errors;
using ControleFinanceiro.Contracts.Financeiro.Faturas;
using ControleFinanceiro.Contracts.Financeiro.ImportacaoFatura;
using ControleFinanceiro.SharedKernel.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControleFinanceiro.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/faturas")]
public sealed class FaturasController(
    FaturaCartaoAppService service,
    ImportacaoFaturaService importacao,
    ICurrentUser currentUser) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(FaturaListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<FaturaListResponse>> Listar(
        [FromQuery] FaturaListQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await service.ListarAsync(query, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(FaturaDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FaturaDetalheResponse>> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        var response = await service.ObterPorIdAsync(id, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }

    [HttpPost("{id:guid}/pagar")]
    [ProducesResponseType(typeof(FaturaDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FaturaDetalheResponse>> Pagar(
        Guid id,
        [FromBody] PagarFaturaRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.PagarAsync(id, request, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }

    [HttpPost("{id:guid}/estornar")]
    [ProducesResponseType(typeof(FaturaDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FaturaDetalheResponse>> Estornar(Guid id, CancellationToken cancellationToken)
    {
        var response = await service.EstornarAsync(id, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }

    // ── Importação de fatura CSV ──────────────────────────────────────────────

    [HttpPost("importar/preview")]
    [RequireFamiliaId]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ImportacaoFaturaPreviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [RequestSizeLimit(5 * 1024 * 1024)] // 5 MB
    public async Task<ActionResult<ImportacaoFaturaPreviewResponse>> Preview(
        [FromQuery] Guid cartaoId,
        IFormFile arquivo,
        CancellationToken cancellationToken)
    {
        if (arquivo is null || arquivo.Length == 0)
            return BadRequestResponse("Nenhum arquivo enviado.", "arquivo");

        var ext = Path.GetExtension(arquivo.FileName).ToLowerInvariant();
        if (ext != ".csv" && ext != ".txt" && ext != ".pdf")
            return BadRequestResponse("Apenas arquivos PDF ou CSV são suportados (.pdf, .csv, .txt).", "arquivo");

        var preview = await importacao.GerarPreviewAsync(cartaoId, arquivo.OpenReadStream(), arquivo.FileName, currentUser.FamiliaId!.Value, cancellationToken);
        return Ok(preview);
    }

    [HttpPost("importar/confirmar")]
    [RequireFamiliaId]
    [ProducesResponseType(typeof(ConfirmarImportacaoFaturaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ConfirmarImportacaoFaturaResponse>> Confirmar(
        [FromBody] ConfirmarImportacaoFaturaRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Itens.Count == 0)
            return BadRequestResponse("Nenhum item para importar.", "itens");

        var resultado = await importacao.ConfirmarAsync(request, currentUser.FamiliaId!.Value, cancellationToken);
        return Ok(resultado);
    }
}
