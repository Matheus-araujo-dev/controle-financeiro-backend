using ControleFinanceiro.Api.Configuration;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.FinanceAI;
using ControleFinanceiro.Contracts.Agente;
using ControleFinanceiro.SharedKernel.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/agente")]
public sealed class AgenteController(
    IFinanceAgentService agentService,
    IAgentFileService fileService,
    FinanceInsightsService insightsService,
    FinanceCategorizacaoService categorizacaoService,
    IAppDbContext db,
    ICurrentUser currentUser) : ApiControllerBase
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

        return Ok(MapResponse(result));
    }

    [HttpPost("perguntar-com-arquivo")]
    [EnableRateLimiting(RateLimitPolicies.AiPolicy)]
    [RequestSizeLimit(134_217_728)]
    [ProducesResponseType(typeof(AgentePerguntarResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AgentePerguntarResponse>> PerguntarComArquivo(
        [FromForm] string mensagem,
        [FromForm] Guid? conversaId,
        IFormFile? arquivo,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mensagem))
            return BadRequestResponse("Mensagem é obrigatória.", "mensagem");

        string? tempPath = null;
        string? fileName = null;

        if (arquivo is { Length: > 0 })
        {
            fileName = arquivo.FileName;
            tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{Path.GetExtension(fileName)}");
            await using var fs = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write);
            await arquivo.CopyToAsync(fs, cancellationToken);
        }

        try
        {
            var agentRequest = new AgentRequest(mensagem, conversaId)
            {
                ArquivoAnexoPath = tempPath,
                ArquivoAnexoNome = fileName
            };

            var result = await agentService.ProcessarAsync(agentRequest, cancellationToken);
            return Ok(MapResponse(result));
        }
        finally
        {
            if (tempPath is not null && System.IO.File.Exists(tempPath))
                System.IO.File.Delete(tempPath);
        }
    }

    [HttpGet("conversas")]
    [ProducesResponseType(typeof(IReadOnlyList<ConversaResumoResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ConversaResumoResponse>>> ListarConversas(CancellationToken cancellationToken)
    {
        _ = Guid.TryParse(currentUser.UserId, out var usuarioId);
        var familiaId = currentUser.FamiliaId ?? Guid.Empty;

        var conversas = await db.AiConversas.AsNoTracking()
            .Where(c => c.FamiliaId == familiaId && c.UsuarioId == usuarioId)
            .OrderByDescending(c => c.CreatedAtUtc)
            .Take(50)
            .Select(c => new
            {
                c.Id,
                c.CreatedAtUtc,
                UltimaMensagem = c.Mensagens.OrderByDescending(m => m.CreatedAtUtc).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var resultado = conversas.Select(c => new ConversaResumoResponse(
            c.Id,
            c.CreatedAtUtc,
            c.UltimaMensagem?.CreatedAtUtc,
            c.UltimaMensagem?.Conteudo != null
                ? (c.UltimaMensagem.Conteudo.Length > 100 ? c.UltimaMensagem.Conteudo[..100] + "..." : c.UltimaMensagem.Conteudo)
                : null,
            0)).ToList();

        return Ok(resultado);
    }

    [HttpGet("conversas/{id:guid}")]
    [ProducesResponseType(typeof(ConversaDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConversaDetalheResponse>> ObterConversa(Guid id, CancellationToken cancellationToken)
    {
        _ = Guid.TryParse(currentUser.UserId, out var usuarioId);
        var familiaId = currentUser.FamiliaId ?? Guid.Empty;

        var conversa = await db.AiConversas.AsNoTracking()
            .Include(c => c.Mensagens)
            .Where(c => c.Id == id && c.FamiliaId == familiaId && c.UsuarioId == usuarioId)
            .FirstOrDefaultAsync(cancellationToken);

        if (conversa is null)
            return NotFoundResponse("Conversa não encontrada.");

        var mensagens = conversa.Mensagens
            .OrderBy(m => m.CreatedAtUtc)
            .Select(m => new ConversaMensagemResponse(m.Id, m.Papel, m.Conteudo, m.CreatedAtUtc))
            .ToList();

        return Ok(new ConversaDetalheResponse(conversa.Id, conversa.CreatedAtUtc, mensagens));
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

    [HttpPost("perguntar-stream")]
    [EnableRateLimiting(RateLimitPolicies.AiPolicy)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task PerguntarStream(
        [FromBody] AgentePerguntarRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Mensagem))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        SetSseHeaders();

        var result = await ExecuteStreamAsync(
            new AgentRequest(request.Mensagem, request.ConversaId), cancellationToken);

        if (result is not null)
        {
            var finalJson = System.Text.Json.JsonSerializer.Serialize(MapResponse(result), SseJsonOptions);
            await Response.WriteAsync($"event: resultado\ndata: {finalJson}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }

    [HttpPost("perguntar-stream-com-arquivo")]
    [EnableRateLimiting(RateLimitPolicies.AiPolicy)]
    [RequestSizeLimit(134_217_728)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task PerguntarStreamComArquivo(
        [FromForm] string mensagem,
        [FromForm] Guid? conversaId,
        IFormFile? arquivo,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mensagem))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        string? tempPath = null;
        string? fileName = null;

        if (arquivo is { Length: > 0 })
        {
            fileName = arquivo.FileName;
            tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{Path.GetExtension(fileName)}");
            await using var fs = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write);
            await arquivo.CopyToAsync(fs, cancellationToken);
        }

        SetSseHeaders();

        try
        {
            var agentRequest = new AgentRequest(mensagem, conversaId)
            {
                ArquivoAnexoPath = tempPath,
                ArquivoAnexoNome = fileName
            };

            var result = await ExecuteStreamAsync(agentRequest, cancellationToken);

            if (result is not null)
            {
                var finalJson = System.Text.Json.JsonSerializer.Serialize(MapResponse(result), SseJsonOptions);
                await Response.WriteAsync($"event: resultado\ndata: {finalJson}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        finally
        {
            if (tempPath is not null && System.IO.File.Exists(tempPath))
                System.IO.File.Delete(tempPath);
        }
    }

    [HttpGet("arquivos/{fileId}/{nomeArquivo}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BaixarArquivo(
        string fileId,
        string nomeArquivo,
        CancellationToken cancellationToken)
    {
        try
        {
            var url = $"/api/v1/agente/arquivos/{fileId}/{nomeArquivo}";
            var bytes = await fileService.LerAsync(url, cancellationToken);
            var mime = InferirMimeType(nomeArquivo);
            return File(bytes, mime, nomeArquivo);
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    private static readonly System.Text.Json.JsonSerializerOptions SseJsonOptions = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
    };

    private void SetSseHeaders()
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        Response.Headers["X-Accel-Buffering"] = "no";
    }

    private async Task<AgentResponse?> ExecuteStreamAsync(AgentRequest agentRequest, CancellationToken cancellationToken)
    {
        async Task WriteSseEvent(AgentStreamEvent evt)
        {
            var dto = new AgenteStreamEventDto(
                evt.Tipo.ToString().ToLowerInvariant(),
                evt.Dados,
                evt.Arquivo is { } a ? new AgentFileOutputDto(a.Nome, a.MimeType, a.Url, a.TamanhoBytes) : null);

            var json = System.Text.Json.JsonSerializer.Serialize(dto, SseJsonOptions);
            await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }

        try
        {
            return await agentService.ProcessarStreamAsync(agentRequest, WriteSseEvent, cancellationToken);
        }
        catch (OperationCanceledException) { return null; }
        catch (Exception ex)
        {
            var errorJson = System.Text.Json.JsonSerializer.Serialize(
                new AgenteStreamEventDto("erro", ex.Message), SseJsonOptions);
            await Response.WriteAsync($"data: {errorJson}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
            return null;
        }
    }

    private static AgentePerguntarResponse MapResponse(AgentResponse result) =>
        new(result.Resposta, result.ConversaId, result.TokensUsados,
            result.Arquivos?.Select(a => new AgentFileOutputDto(a.Nome, a.MimeType, a.Url, a.TamanhoBytes)).ToList());

    private static string InferirMimeType(string nome) =>
        Path.GetExtension(nome).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".pdf" => "application/pdf",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".csv" => "text/csv",
            _ => "application/octet-stream"
        };
}
