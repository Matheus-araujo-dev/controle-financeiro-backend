using System.Text.Json;
using ControleFinanceiro.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class ListarMeiosPagamentoTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "listar_meios_pagamento";
    public string Description => "Lista as formas de pagamento e cartões cadastrados na família. Use para obter formaPagamentoId e cartaoId antes de criar um lançamento.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {},
          "required": []
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        var formas = await db.FormasPagamento
            .AsNoTracking()
            .Where(f => f.Ativo)
            .OrderBy(f => f.Nome)
            .Select(f => new { id = f.Id, nome = f.Nome, tipo = f.Tipo.ToString(), ehCartao = f.EhCartao })
            .ToListAsync(cancellationToken);

        var cartoes = await db.Cartoes
            .AsNoTracking()
            .Where(c => c.Ativo)
            .OrderBy(c => c.Nome)
            .Select(c => new { id = c.Id, nome = c.Nome, bandeira = c.Bandeira.ToString() })
            .ToListAsync(cancellationToken);

        return JsonSerializer.Serialize(new { formasPagamento = formas, cartoes });
    }
}
