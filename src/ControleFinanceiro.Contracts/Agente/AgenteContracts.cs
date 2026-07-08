using System.ComponentModel.DataAnnotations;

namespace ControleFinanceiro.Contracts.Agente;

public sealed record AgentePerguntarRequest(
    // Limita a entrada enviada ao LLM: evita abuso e custo de tokens descontrolado.
    [Required, StringLength(2000, MinimumLength = 1)] string Mensagem,
    Guid? ConversaId = null);

public sealed record AgentePerguntarResponse(
    string Resposta,
    Guid ConversaId,
    int TokensUsados);

public sealed record AgenteInsightsRequest(
    [Required, RegularExpression(@"^\d{4}-\d{2}$")] string MesReferencia);

public sealed record AgenteInsight(string Tipo, string Mensagem, string? Valor = null);

public sealed record AgenteInsightsResponse(IReadOnlyList<AgenteInsight> Insights, int TokensUsados);

public sealed record AgenteCategorizarRequest(
    [Required, MaxLength(100)] IReadOnlyList<string> Descricoes);

public sealed record AgenteCategorizacaoItem(
    string Descricao,
    Guid? ContaGerencialId,
    string? ContaGerencialDescricao,
    double Confianca);

public sealed record AgenteCategorizarResponse(IReadOnlyList<AgenteCategorizacaoItem> Itens);
