using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.FinanceAI;

public sealed class AiToolCall : AuditableEntity
{
    private AiToolCall() { }

    public Guid ConversaId { get; private set; }
    public string NomeFerramenta { get; private set; } = string.Empty;
    public string InputJson { get; private set; } = string.Empty;
    public string OutputJson { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public int TokensEntrada { get; private set; }
    public int TokensSaida { get; private set; }

    public static AiToolCall Criar(
        Guid conversaId,
        string nomeFerramenta,
        string inputJson,
        string outputJson,
        string status,
        int tokensEntrada,
        int tokensSaida)
    {
        return new AiToolCall
        {
            Id = Guid.NewGuid(),
            ConversaId = conversaId,
            NomeFerramenta = nomeFerramenta,
            InputJson = inputJson,
            OutputJson = outputJson,
            Status = status,
            TokensEntrada = tokensEntrada,
            TokensSaida = tokensSaida
        };
    }
}
