using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Financeiro;
using ControleFinanceiro.Domain.Identidade;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class BuscarGastosPorResponsavelTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "buscar_gastos_por_responsavel";
    public string Description => "Retorna gastos de um responsável em um período. Administrador pode consultar qualquer pessoa; Membro deve informar o próprio responsavelId.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "mes": { "type": "integer", "description": "Mês (1-12)" },
            "ano": { "type": "integer", "description": "Ano (ex: 2026)" },
            "responsavelId": { "type": "string", "description": "ID da pessoa (opcional). Se omitido, retorna todos (apenas Administrador)." }
          },
          "required": ["mes", "ano"]
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        var input = JsonNode.Parse(inputJson);
        var mes = input?["mes"]?.GetValue<int>() ?? DateTime.UtcNow.Month;
        var ano = input?["ano"]?.GetValue<int>() ?? DateTime.UtcNow.Year;
        var responsavelIdStr = input?["responsavelId"]?.GetValue<string>();
        Guid? responsavelId = Guid.TryParse(responsavelIdStr, out var rid) ? rid : null;

        // Membro deve informar responsavelId (próprio) — não pode listar todos
        if (context.Papel != PapelFamilia.Administrador && !responsavelId.HasValue)
            return JsonSerializer.Serialize(new { erro = "Informe o responsavelId para consultar seus gastos." });

        var inicio = new DateOnly(ano, mes, 1);
        var fim = inicio.AddMonths(1).AddDays(-1);

        var query =
            from cp in db.ContasPagar.AsNoTracking()
            join p in db.Pessoas on cp.ResponsavelCompraId equals p.Id into pj
            from p in pj.DefaultIfEmpty()
            where cp.DataVencimento >= inicio && cp.DataVencimento <= fim
               && cp.StatusContaId != StatusConta.CanceladaId
            select new { cp, NomeResponsavel = p != null ? p.Nome : "Sem responsável" };

        if (responsavelId.HasValue)
            query = query.Where(x => x.cp.ResponsavelCompraId == responsavelId.Value);

        var gastos = await query
            .GroupBy(x => new { x.cp.ResponsavelCompraId, x.NomeResponsavel })
            .Select(g => new
            {
                responsavelId = g.Key.ResponsavelCompraId,
                nome = g.Key.NomeResponsavel,
                total = g.Sum(x => x.cp.ValorLiquido),
                quantidadeLancamentos = g.Count()
            })
            .OrderByDescending(g => g.total)
            .ToListAsync(cancellationToken);

        return JsonSerializer.Serialize(new
        {
            periodo = $"{mes:D2}/{ano}",
            totalGeral = gastos.Sum(g => g.total),
            responsaveis = gastos
        });
    }
}
