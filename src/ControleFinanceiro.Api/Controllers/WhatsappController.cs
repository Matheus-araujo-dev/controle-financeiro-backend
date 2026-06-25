using ControleFinanceiro.Api.Filters;
using ControleFinanceiro.Application.FinanceAI;
using ControleFinanceiro.Contracts.Agente;
using Microsoft.AspNetCore.Mvc;

namespace ControleFinanceiro.Api.Controllers;

[ApiController]
[Route("api/v1/agente/whatsapp")]
public sealed class WhatsappController(IWhatsappMensagemService mensagemService) : ApiControllerBase
{
    /// <summary>
    /// Endpoint interno chamado exclusivamente pela bridge Node.js (Baileys).
    /// Autenticado por API key interna + HMAC — não expor publicamente.
    /// </summary>
    [HttpPost("mensagem")]
    [InternalApiKey]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType(typeof(WhatsappMensagemInboundResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<WhatsappMensagemInboundResponse>> ReceberMensagem(
        [FromBody] WhatsappMensagemInboundRequest request,
        CancellationToken cancellationToken)
    {
        var appRequest = new WhatsappMensagemRequest(
            request.Telefone,
            request.Tipo,
            request.Texto,
            request.MidiaBase64,
            request.MimeType,
            request.NomeArquivo,
            request.MessageId,
            request.Timestamp);

        var resultado = await mensagemService.ProcessarAsync(appRequest, cancellationToken);

        if (resultado is null)
            return Ok(new WhatsappMensagemInboundResponse([]));

        var respostas = resultado.Respostas
            .Select(r => new WhatsappRespostaDto(r.Tipo, r.Conteudo, r.Opcoes))
            .ToList();

        return Ok(new WhatsappMensagemInboundResponse(respostas));
    }
}
