using System.Text.Json;
using ControleFinanceiro.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class ListarPessoasTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "listar_pessoas";
    public string Description => "Lista as pessoas cadastradas na família (responsáveis de compra e fornecedores/recebedores). Use para obter IDs antes de criar um lançamento.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "nome": { "type": "string", "description": "Filtro parcial por nome (opcional)." }
          },
          "required": []
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        var filtroNome = System.Text.Json.Nodes.JsonNode.Parse(inputJson)?["nome"]?.GetValue<string>();

        var query = db.Pessoas.AsNoTracking().Where(p => p.Ativo);

        if (!string.IsNullOrWhiteSpace(filtroNome))
            query = query.Where(p => p.Nome.Contains(filtroNome));

        var pessoas = await query
            .OrderBy(p => p.Nome)
            .Select(p => new { id = p.Id, nome = p.Nome, tipo = p.TipoPessoa.ToString() })
            .ToListAsync(cancellationToken);

        return JsonSerializer.Serialize(new { total = pessoas.Count, pessoas });
    }
}
