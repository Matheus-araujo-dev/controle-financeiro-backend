using ControleFinanceiro.Application.Conciliacao;
using ControleFinanceiro.Contracts.Conciliacao;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControleFinanceiro.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/faturas/{faturaId:guid}/conciliacoes")]
public sealed class ConciliacoesFaturaController(ConciliacaoFaturaAppService service, CriarItemFaturaAppService criacao) : ApiControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [ProducesResponseType(typeof(ConciliacaoFaturaResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ConciliacaoFaturaResponse>> Iniciar(Guid faturaId, IFormFile arquivo, CancellationToken ct)
    {
        await using var stream = arquivo.OpenReadStream();
        var result = await service.IniciarAsync(faturaId, arquivo.FileName, stream, ct);
        return result is null ? NotFoundResponse() : Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ConciliacaoFaturaResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ConciliacaoFaturaResponse>> Obter(Guid faturaId, Guid id, CancellationToken ct)
    {
        var result = await service.ObterAsync(faturaId, id, ct);
        return result is null ? NotFoundResponse() : Ok(result);
    }

    [HttpPost("{id:guid}/itens/{itemId:guid}/vincular")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Vincular(Guid faturaId, Guid id, Guid itemId, VincularItemFaturaRequest request, CancellationToken ct)
        => await service.VincularAsync(faturaId, id, itemId, request, ct) ? NoContent() : NotFoundResponse();

    [HttpPost("{id:guid}/itens/{itemId:guid}/criar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Criar(Guid faturaId, Guid id, Guid itemId, CriarItemFaturaRequest request, CancellationToken ct)
        => await criacao.CriarAsync(faturaId, id, itemId, request, ct) ? NoContent() : NotFoundResponse();

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RevisaoFaturaResumoResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RevisaoFaturaResumoResponse>>> Listar(Guid faturaId, CancellationToken ct)
        => Ok(await service.ListarAsync(faturaId, ct));

    [HttpPut("{id:guid}/itens/{itemId:guid}/rascunho")]
    [ProducesResponseType(typeof(RascunhoFaturaResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RascunhoFaturaResponse>> Rascunho(Guid faturaId, Guid id, Guid itemId, RascunhoFaturaRequest request, CancellationToken ct)
    {
        var result = await service.SalvarRascunhoAsync(faturaId, id, itemId, request, ct);
        return result is null ? NotFoundResponse() : Ok(result);
    }

    [HttpPost("{id:guid}/itens/{itemId:guid}/previa-reembolso")]
    [ProducesResponseType(typeof(IReadOnlyList<PreviaReembolsoFaturaResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PreviaReembolsoFaturaResponse>>> PreviaReembolso(
        Guid faturaId, Guid id, Guid itemId, PreviaReembolsoFaturaRequest request, CancellationToken ct)
    {
        var result = await service.PreviaReembolsoAsync(faturaId, id, itemId, request, ct);
        return result is null ? NotFoundResponse() : Ok(result);
    }

    [HttpPost("{id:guid}/itens/{itemId:guid}/ignorar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Ignorar(Guid faturaId, Guid id, Guid itemId, CancellationToken ct)
        => await service.IgnorarAsync(faturaId, id, itemId, ct) ? NoContent() : NotFoundResponse();
}
