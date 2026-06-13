using System.Text.Json;
using ControleFinanceiro.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class ListarCategoriasTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "listar_categorias";
    public string Description => "Lista todas as categorias financeiras (contas gerenciais) ativas da família. Use antes de sugerir ou classificar um lançamento para garantir que a categoria existe.";
    public string InputSchema => """{"type":"object","properties":{},"required":[]}""";

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        var categorias = await db.ContasGerenciais
            .AsNoTracking()
            .Where(c => c.Ativo)
            .OrderBy(c => c.Descricao)
            .Select(c => new { c.Id, c.Descricao, c.Codigo, c.Tipo })
            .ToListAsync(cancellationToken);

        return JsonSerializer.Serialize(new
        {
            total = categorias.Count,
            categorias = categorias.Select(c => new
            {
                id = c.Id,
                descricao = c.Descricao,
                codigo = c.Codigo,
                tipo = c.Tipo.ToString()
            })
        });
    }
}
