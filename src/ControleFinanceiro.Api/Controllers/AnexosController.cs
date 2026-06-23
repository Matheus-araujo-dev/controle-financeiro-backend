using ControleFinanceiro.Application.Anexos;
using ControleFinanceiro.Contracts.Anexos;
using ControleFinanceiro.Contracts.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ControleFinanceiro.Api.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting("Relaxed")]
[Route("api/v1/anexos")]
public sealed class AnexosController(AnexoAppService service) : ApiControllerBase
{
    [HttpGet("{tipoEntidade}/{entidadeId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyCollection<AnexoResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<AnexoResponse>>> Listar(
        string tipoEntidade,
        Guid entidadeId,
        CancellationToken cancellationToken)
    {
        var response = await service.ListarAsync(tipoEntidade, entidadeId, cancellationToken);
        return response is null ? NotFoundResponse() : Ok(response);
    }

    [HttpPost("{tipoEntidade}/{entidadeId:guid}")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    [ProducesResponseType(typeof(AnexoResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnexoResponse>> Adicionar(
        string tipoEntidade,
        Guid entidadeId,
        [FromForm] IFormFile arquivo,
        CancellationToken cancellationToken)
    {
        if (arquivo is null) return BadRequest("Nenhum arquivo enviado.");

        await using var stream = arquivo.OpenReadStream();
        var response = await service.AdicionarAsync(
            tipoEntidade,
            entidadeId,
            arquivo.FileName,
            arquivo.ContentType,
            arquivo.Length,
            stream,
            cancellationToken);

        return response is null
            ? NotFoundResponse()
            : Created(response.UrlConteudo, response);
    }

    [HttpGet("{anexoId:guid}/conteudo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterConteudo(Guid anexoId, CancellationToken cancellationToken)
    {
        var response = await service.ObterConteudoAsync(anexoId, cancellationToken);
        return response is null
            ? NotFoundResponse()
            : File(response.Conteudo, response.MimeType, response.NomeArquivo, enableRangeProcessing: true);
    }

    [HttpDelete("{tipoEntidade}/{entidadeId:guid}/{anexoId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Excluir(
        string tipoEntidade,
        Guid entidadeId,
        Guid anexoId,
        CancellationToken cancellationToken)
    {
        return await service.ExcluirAsync(tipoEntidade, entidadeId, anexoId, cancellationToken)
            ? NoContent()
            : NotFoundResponse();
    }
}
