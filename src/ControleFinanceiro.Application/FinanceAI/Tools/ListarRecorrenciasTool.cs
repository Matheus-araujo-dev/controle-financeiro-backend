using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class ListarRecorrenciasTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "listar_recorrencias";
    public string Description => "Lista as regras de recorrência (contas que se repetem todo mês). Mostra descrição, valor, tipo (pagar/receber), dia do vencimento e status (ativa/pausada/encerrada). Pode filtrar por ativas ou tipo.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "apenasAtivas": { "type": "boolean", "description": "Se true, retorna apenas recorrências ativas (padrão true)" },
            "tipo":         { "type": "string", "enum": ["pagar", "receber"], "description": "Filtrar por tipo (opcional)" }
          }
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        var input = JsonNode.Parse(inputJson);
        var apenasAtivas = input?["apenasAtivas"]?.GetValue<bool>() ?? true;
        var tipoStr = input?["tipo"]?.GetValue<string>();

        var query = db.RegrasRecorrencia.AsNoTracking().AsQueryable();

        if (apenasAtivas)
            query = query.Where(r => r.Ativa);

        if (tipoStr == "pagar")
            query = query.Where(r => r.TipoLancamento == TipoLancamentoRecorrencia.ContaPagar);
        else if (tipoStr == "receber")
            query = query.Where(r => r.TipoLancamento == TipoLancamentoRecorrencia.ContaReceber);

        var regras = await query
            .OrderBy(r => r.TipoLancamento)
            .ThenBy(r => r.DiaOrdemMensal)
            .ToListAsync(cancellationToken);

        var resultado = regras.Select(r =>
        {
            var template = TryParseTemplate(r.TemplateJson);
            return new
            {
                id = r.Id,
                tipo = r.TipoLancamento == TipoLancamentoRecorrencia.ContaPagar ? "Pagar" : "Receber",
                descricao = template.Descricao,
                valor = template.Valor,
                diaVencimento = r.DiaOrdemMensal,
                tipoDia = r.TipoDia == TipoDiaRecorrencia.DiaUtil ? "Dia útil" : "Dia fixo",
                dataInicio = r.DataInicio.ToString("dd/MM/yyyy"),
                dataFim = r.DataFim?.ToString("dd/MM/yyyy"),
                status = r.Encerrada ? "Encerrada" : r.Ativa ? "Ativa" : "Pausada",
                observacao = r.Observacao
            };
        }).ToList();

        return JsonSerializer.Serialize(new
        {
            total = resultado.Count,
            recorrencias = resultado
        });
    }

    private static (string? Descricao, decimal Valor) TryParseTemplate(string templateJson)
    {
        try
        {
            var node = JsonNode.Parse(templateJson);
            var descricao = node?["Descricao"]?.GetValue<string>();
            var valor = node?["ValorOriginal"]?.GetValue<decimal>() ?? 0;
            return (descricao, valor);
        }
        catch
        {
            return (null, 0);
        }
    }
}
