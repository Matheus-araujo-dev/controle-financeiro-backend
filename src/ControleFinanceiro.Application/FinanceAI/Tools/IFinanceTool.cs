using ControleFinanceiro.Application.FinanceAI;
using ControleFinanceiro.Domain.Identidade;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed record ToolContext(
    Guid FamiliaId,
    Guid UsuarioId,
    PapelFamilia Papel,
    string NomeFamilia,
    string? ArquivoAnexoPath = null,
    string? ArquivoAnexoNome = null,
    IAgentFileService? FileService = null)
{
    public List<AgentFileOutput> ArquivosGerados { get; } = [];
}

public interface IFinanceTool
{
    string Name { get; }
    string Description { get; }
    string InputSchema { get; }
    Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken);
}
