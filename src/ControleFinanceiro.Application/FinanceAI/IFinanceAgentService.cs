using ControleFinanceiro.Domain.FinanceAI;

namespace ControleFinanceiro.Application.FinanceAI;

public interface IFinanceAgentService
{
    Task<AgentResponse> ProcessarAsync(AgentRequest request, CancellationToken cancellationToken);
}

public sealed record AgentRequest(string Mensagem, Guid? ConversaId = null)
{
    /// <summary>MessageId externo (WhatsApp) para idempotência.</summary>
    public string? ExternalMessageId { get; init; }

    /// <summary>Sobrescreve ICurrentUser quando a chamada não vem de um HttpContext (canal WhatsApp).</summary>
    public Guid? UsuarioId { get; init; }

    /// <summary>Sobrescreve ICurrentUser.FamiliaId quando a chamada não vem de um HttpContext.</summary>
    public Guid? FamiliaId { get; init; }

    /// <summary>Canal de origem (Web por padrão).</summary>
    public CanalAi Canal { get; init; } = CanalAi.Web;

    /// <summary>Contato externo (número de telefone para canal WhatsApp).</summary>
    public string? ContatoExterno { get; init; }

    /// <summary>Anexo recebido junto da mensagem e ainda não vinculado ao lançamento.</summary>
    public AgentAttachment? Anexo { get; init; }
}

public sealed record AgentAttachment(string NomeArquivo, string MimeType, string ArquivoBase64);

public sealed record AgentResponse(
    string Resposta,
    Guid ConversaId,
    int TokensUsados);
