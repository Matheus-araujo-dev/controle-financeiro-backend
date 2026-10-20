namespace ControleFinanceiro.Application.FinanceAI;

public sealed record AgentStreamEvent(AgentStreamEventType Tipo, string? Dados = null, AgentFileOutput? Arquivo = null);

public enum AgentStreamEventType
{
    Processando,
    Ferramenta,
    Texto,
    Arquivo,
    Concluido,
    Erro
}
