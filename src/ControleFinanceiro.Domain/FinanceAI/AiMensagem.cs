using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.FinanceAI;

public sealed class AiMensagem : AuditableEntity
{
    private AiMensagem() { }

    public Guid ConversaId { get; private set; }
    public string Papel { get; private set; } = string.Empty;
    public string Conteudo { get; private set; } = string.Empty;
    public string? ExternalMessageId { get; private set; }

    public static AiMensagem Criar(Guid conversaId, string papel, string conteudo, string? externalMessageId = null)
    {
        return new AiMensagem
        {
            Id = Guid.NewGuid(),
            ConversaId = conversaId,
            Papel = papel,
            Conteudo = conteudo,
            ExternalMessageId = externalMessageId
        };
    }
}
