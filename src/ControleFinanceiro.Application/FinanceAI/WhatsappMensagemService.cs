using System.Text;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.FinanceAI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ControleFinanceiro.Application.FinanceAI;

public sealed class WhatsappMensagemService(
    IAppDbContext db,
    IFinanceAgentService agentService,
    ITranscricaoAudioService transcricaoService,
    IExtracaoImagemFinanceiroService extracaoImagemService,
    ILogger<WhatsappMensagemService> logger) : IWhatsappMensagemService
{
    public async Task<WhatsappMensagemResponse?> ProcessarAsync(
        WhatsappMensagemRequest request, CancellationToken cancellationToken)
    {
        var telefone = WhatsappUsuario.NormalizarTelefone(request.Telefone);

        var wup = await db.WhatsappUsuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Telefone == telefone && w.Ativo, cancellationToken);

        if (wup is null)
        {
            logger.LogDebug("Telefone {Telefone} não cadastrado ou inativo — mensagem ignorada.", telefone);
            return null;
        }
        AgentAttachment? anexo = null;

        var jaProcessado = await db.AiMensagens
            .AsNoTracking()
            .AnyAsync(m => m.ExternalMessageId == request.MessageId, cancellationToken);

        if (jaProcessado)
        {
            logger.LogDebug("MessageId {MessageId} já processado — ignorando.", request.MessageId);
            return null;
        }

        string? textoFinal;

        switch (request.Tipo)
        {
            case "texto":
                if (string.IsNullOrWhiteSpace(request.Texto))
                    return RespostaTexto("Não consegui ler sua mensagem. Tente novamente.");
                textoFinal = request.Texto;
                break;

            case "audio":
                textoFinal = await ProcessarAudioAsync(request, cancellationToken);
                if (textoFinal is null)
                    return RespostaTexto("Recebi seu áudio, mas não consegui transcrever. Tente enviar sua mensagem em texto.");
                break;

            case "imagem":
                var textoImagem = await ProcessarImagemAsync(request, cancellationToken);
                if (textoImagem is null)
                    return RespostaTexto("Recebi sua imagem, mas não consegui extrair os dados. Descreva o lançamento em texto para eu registrar.");
                textoFinal = textoImagem;
                anexo = CriarAnexoDaMensagem(request);
                break;

            default:
                return RespostaTexto("Por enquanto só processo mensagens de texto, áudio e foto de comprovante.");
        }

        var conversaId = await ObterConversaAtivaAsync(wup, cancellationToken);

        var agentRequest = new AgentRequest(textoFinal, conversaId)
        {
            ExternalMessageId = request.MessageId,
            UsuarioId = wup.UsuarioId,
            FamiliaId = wup.FamiliaId,
            Canal = CanalAi.WhatsApp,
            ContatoExterno = wup.Telefone,
            Anexo = anexo
        };

        var agentResponse = await agentService.ProcessarAsync(agentRequest, cancellationToken);

        return new WhatsappMensagemResponse(
        [
            new WhatsappResposta("texto", agentResponse.Resposta)
        ]);
    }

    private async Task<string?> ProcessarAudioAsync(WhatsappMensagemRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.MidiaBase64))
            return null;

        logger.LogDebug("Transcrevendo áudio {MessageId} ({MimeType})", request.MessageId, request.MimeType);

        var transcricao = await transcricaoService.TranscreverAsync(
            request.MidiaBase64, request.MimeType ?? "audio/ogg", ct);

        if (string.IsNullOrWhiteSpace(transcricao))
            return null;

        logger.LogDebug("Áudio {MessageId} transcrito com {Length} caracteres.", request.MessageId, transcricao.Length);
        return transcricao;
    }

    private async Task<string?> ProcessarImagemAsync(WhatsappMensagemRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.MidiaBase64))
            return null;

        logger.LogDebug("Extraindo dados de imagem {MessageId} ({MimeType})", request.MessageId, request.MimeType);

        var resultado = await extracaoImagemService.ExtrairAsync(
            request.MidiaBase64, request.MimeType ?? "image/jpeg", ct);

        if (!resultado.Sucesso)
            return null;

        var sb = new StringBuilder("Recebi uma foto de comprovante com os seguintes dados:\n");

        if (!string.IsNullOrEmpty(resultado.Estabelecimento))
            sb.AppendLine($"- Estabelecimento: {resultado.Estabelecimento}");
        if (resultado.Valor.HasValue)
            sb.AppendLine($"- Valor: R$ {resultado.Valor.Value:F2}");
        if (resultado.Data.HasValue)
            sb.AppendLine($"- Data: {resultado.Data.Value:dd/MM/yyyy}");
        if (!string.IsNullOrEmpty(resultado.Descricao))
            sb.AppendLine($"- Descrição: {resultado.Descricao}");
        if (!string.IsNullOrEmpty(resultado.MeioPagamento))
            sb.AppendLine($"- Meio de pagamento: {resultado.MeioPagamento}");
        if (resultado.QuantidadeParcelas.HasValue)
            sb.AppendLine($"- Parcelas: {resultado.QuantidadeParcelas.Value}");
        if (!string.IsNullOrEmpty(resultado.FinalCartao))
            sb.AppendLine($"- Final do cartao: {resultado.FinalCartao}");
        if (!string.IsNullOrEmpty(resultado.BandeiraCartao))
            sb.AppendLine($"- Bandeira: {resultado.BandeiraCartao}");


        sb.Append("Por favor, crie um lançamento com esses dados. Se precisar de mais informações (como categoria), me pergunte.");

        return sb.ToString();
    }

    private static readonly TimeSpan InativiadeConversa = TimeSpan.FromHours(2);

    private async Task<Guid?> ObterConversaAtivaAsync(WhatsappUsuario wup, CancellationToken ct)
    {
        var conversaId = await db.AiConversas
            .Where(c => c.UsuarioId == wup.UsuarioId
                     && c.Canal == CanalAi.WhatsApp
                     && c.ContatoExterno == wup.Telefone)
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c => c.Id)
            .FirstOrDefaultAsync(ct);

        if (conversaId == Guid.Empty) return null;

        // Verifica se a conversa ainda está ativa pela última mensagem
        var limite = DateTimeOffset.UtcNow.Subtract(InativiadeConversa);
        var ultimaMensagemEm = await db.AiMensagens
            .Where(m => m.ConversaId == conversaId)
            .MaxAsync(m => (DateTimeOffset?)m.CreatedAtUtc, ct);

        if (ultimaMensagemEm is null || ultimaMensagemEm < limite)
        {
            logger.LogDebug("Conversa {Id} inativa há mais de {Horas}h — iniciando nova.", conversaId, InativiadeConversa.TotalHours);
            return null;
        }

        return conversaId;
    }

    private static AgentAttachment CriarAnexoDaMensagem(WhatsappMensagemRequest request)
    {
        return new AgentAttachment(
            string.IsNullOrWhiteSpace(request.NomeArquivo)
                ? CriarNomeArquivoPadrao(request.MessageId, request.MimeType)
                : request.NomeArquivo.Trim(),
            request.MimeType ?? "image/jpeg",
            request.MidiaBase64!);
    }

    private static string CriarNomeArquivoPadrao(string messageId, string? mimeType)
    {
        var extension = mimeType?.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/webp" => ".webp",
            "application/pdf" => ".pdf",
            _ => ".jpg"
        };
        var safeMessageId = new string(messageId.Select(character => char.IsLetterOrDigit(character) ? character : '-').ToArray());
        if (string.IsNullOrWhiteSpace(safeMessageId)) safeMessageId = Guid.NewGuid().ToString("N");
        return $"comprovante-{safeMessageId}{extension}";
    }

    private static WhatsappMensagemResponse RespostaTexto(string texto) =>
        new([new WhatsappResposta("texto", texto)]);
}
