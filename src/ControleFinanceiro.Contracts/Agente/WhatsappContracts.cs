namespace ControleFinanceiro.Contracts.Agente;

public sealed record WhatsappMensagemInboundRequest(
    string Telefone,
    string Tipo,
    string? Texto,
    string? MidiaBase64,
    string? MimeType,
    string? NomeArquivo,
    string MessageId,
    DateTimeOffset Timestamp);

public sealed record WhatsappRespostaDto(string Tipo, string Conteudo, IReadOnlyList<string>? Opcoes = null);

public sealed record WhatsappMensagemInboundResponse(IReadOnlyList<WhatsappRespostaDto> Respostas);
