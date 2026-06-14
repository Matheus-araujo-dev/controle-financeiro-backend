namespace ControleFinanceiro.Contracts.Agente;

public sealed record AgentePerquntarRequest(
    string Mensagem,
    Guid? ConversaId = null);

public sealed record AgentePerguntarResponse(
    string Resposta,
    Guid ConversaId,
    int TokensUsados);

public sealed record AgenteInsightsRequest(string MesReferencia);

public sealed record AgenteInsight(string Tipo, string Mensagem, string? Valor = null);

public sealed record AgenteInsightsResponse(IReadOnlyList<AgenteInsight> Insights, int TokensUsados);

public sealed record AgenteCategorizarRequest(IReadOnlyList<string> Descricoes);

public sealed record AgenteCategorizacaoItem(
    string Descricao,
    Guid? ContaGerencialId,
    string? ContaGerencialDescricao,
    double Confianca);

public sealed record AgenteCategorizarResponse(IReadOnlyList<AgenteCategorizacaoItem> Itens);
