namespace ControleFinanceiro.Contracts.Agente;

public sealed record WhatsappPerfilResponse(
    string? Telefone,
    bool Ativo,
    DateTimeOffset? VerificadoEm);

public sealed record WhatsappRegistrarRequest(string Telefone);
