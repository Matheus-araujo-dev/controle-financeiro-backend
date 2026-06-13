namespace ControleFinanceiro.Contracts.Agente;

public sealed record AgentePerquntarRequest(
    string Mensagem,
    Guid? ConversaId = null);

public sealed record AgentePerguntarResponse(
    string Resposta,
    Guid ConversaId,
    int TokensUsados);
