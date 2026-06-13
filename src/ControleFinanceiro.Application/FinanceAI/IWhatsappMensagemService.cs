namespace ControleFinanceiro.Application.FinanceAI;

public sealed record WhatsappMensagemRequest(
    string Telefone,
    string Tipo,           // "texto" | "audio" | "imagem" | "documento"
    string? Texto,
    string? MidiaBase64,
    string? MimeType,
    string MessageId,
    DateTimeOffset Timestamp);

public sealed record WhatsappResposta(string Tipo, string Conteudo, IReadOnlyList<string>? Opcoes = null);

public sealed record WhatsappMensagemResponse(IReadOnlyList<WhatsappResposta> Respostas);

public interface IWhatsappMensagemService
{
    Task<WhatsappMensagemResponse?> ProcessarAsync(WhatsappMensagemRequest request, CancellationToken cancellationToken);
}
