using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Identidade;
using ControleFinanceiro.Domain.PlanejamentoCompras;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class GerenciarCompraPlaneiadaTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "gerenciar_compra_planejada";
    public string Description => "Gerencia compras planejadas: listar, criar, atualizar, marcar como comprada, cancelar ou reverter para planejada.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "acao":                        { "type": "string",  "enum": ["listar", "criar", "atualizar", "comprar", "cancelar", "reverter"], "description": "Ação a executar" },
            "compraId":                    { "type": "string",  "description": "ID da compra (obrigatório para atualizar/comprar/cancelar/reverter)" },
            "titulo":                      { "type": "string",  "description": "Título da compra" },
            "descricao":                   { "type": "string",  "description": "Descrição (opcional)" },
            "valorEstimado":               { "type": "number",  "description": "Valor estimado em reais" },
            "dataDesejada":                { "type": "string",  "description": "Data desejada YYYY-MM-DD (opcional)" },
            "prioridade":                  { "type": "string",  "enum": ["baixa", "media", "alta"], "description": "Prioridade (padrão: media)" },
            "parcelavel":                  { "type": "boolean", "description": "Se pode ser parcelado (padrão: false)" },
            "quantidadeParcelasDesejada":  { "type": "integer", "description": "Número de parcelas desejadas (>=2, se parcelável)" },
            "contaGerencialId":            { "type": "string",  "description": "ID da categoria" },
            "pagadorId":               { "type": "string",  "description": "ID do pagador" },
            "link":                        { "type": "string",  "description": "URL do produto (opcional)" },
            "observacao":                  { "type": "string",  "description": "Observação (opcional)" }
          },
          "required": ["acao"]
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        if (context.Papel == PapelFamilia.Visualizador)
            return JsonSerializer.Serialize(new { erro = "Visualizadores não podem gerenciar compras planejadas." });

        var input = JsonNode.Parse(inputJson);
        var acao = input?["acao"]?.GetValue<string>();

        try
        {
            return acao switch
            {
                "listar" => await ListarAsync(cancellationToken),
                "criar" => await CriarAsync(input!, context, cancellationToken),
                "atualizar" => await AtualizarAsync(input!, cancellationToken),
                "comprar" => await AlterarStatusAsync(input!, "comprar", cancellationToken),
                "cancelar" => await AlterarStatusAsync(input!, "cancelar", cancellationToken),
                "reverter" => await AlterarStatusAsync(input!, "reverter", cancellationToken),
                _ => JsonSerializer.Serialize(new { erro = "Ação inválida. Use: listar, criar, atualizar, comprar, cancelar ou reverter." })
            };
        }
        catch (InvalidOperationException ex)
        {
            return JsonSerializer.Serialize(new { erro = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return JsonSerializer.Serialize(new { erro = ex.Message });
        }
    }

    private async Task<string> ListarAsync(CancellationToken ct)
    {
        var compras = await db.ComprasPlanejadas
            .AsNoTracking()
            .OrderByDescending(c => c.DataDesejada)
            .ThenBy(c => c.Titulo)
            .Select(c => new
            {
                c.Id, c.Titulo, c.Descricao, c.ValorEstimado,
                c.DataDesejada, c.Prioridade, c.Status,
                c.Parcelavel, c.QuantidadeParcelasDesejada
            })
            .ToListAsync(ct);

        return JsonSerializer.Serialize(new
        {
            total = compras.Count,
            compras = compras.Select(c => new
            {
                id = c.Id,
                titulo = c.Titulo,
                descricao = c.Descricao,
                valorEstimado = c.ValorEstimado,
                dataDesejada = c.DataDesejada?.ToString("yyyy-MM-dd"),
                prioridade = c.Prioridade.ToString(),
                status = c.Status.ToString(),
                parcelavel = c.Parcelavel,
                parcelas = c.QuantidadeParcelasDesejada
            })
        });
    }

    private async Task<string> CriarAsync(JsonNode input, ToolContext context, CancellationToken ct)
    {
        var titulo = input["titulo"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(titulo))
            return JsonSerializer.Serialize(new { erro = "Título é obrigatório." });

        var valorEstimado = input["valorEstimado"]?.GetValue<decimal>() ?? 0;
        if (valorEstimado <= 0)
            return JsonSerializer.Serialize(new { erro = "Valor estimado deve ser maior que zero." });

        if (!Guid.TryParse(input["contaGerencialId"]?.GetValue<string>(), out var contaGerencialId))
            return JsonSerializer.Serialize(new { erro = "contaGerencialId é obrigatório." });

        var pagadorId = Guid.TryParse(input["pagadorId"]?.GetValue<string>(), out var rId)
            ? rId : context.UsuarioId;

        var prioridade = MapearPrioridade(input["prioridade"]?.GetValue<string>()) ?? PrioridadePlanejamentoCompra.Media;
        var parcelavel = input["parcelavel"]?.GetValue<bool>() ?? false;
        var parcelas = input["quantidadeParcelasDesejada"]?.GetValue<int>();
        DateOnly? dataDesejada = DateOnly.TryParse(input["dataDesejada"]?.GetValue<string>(), out var dd) ? dd : null;

        var compra = PlanejamentoCompra.Criar(
            titulo,
            input["descricao"]?.GetValue<string>(),
            valorEstimado, dataDesejada, prioridade,
            StatusPlanejamentoCompra.Planejada,
            parcelavel, parcelas,
            contaGerencialId, pagadorId,
            input["link"]?.GetValue<string>(),
            input["observacao"]?.GetValue<string>());

        db.ComprasPlanejadas.Add(compra);
        await db.SaveChangesAsync(ct);

        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            compraId = compra.Id,
            mensagem = $"Compra planejada '{titulo}' (R$ {valorEstimado:N2}) criada com sucesso."
        });
    }

    private async Task<string> AtualizarAsync(JsonNode input, CancellationToken ct)
    {
        if (!Guid.TryParse(input["compraId"]?.GetValue<string>(), out var id))
            return JsonSerializer.Serialize(new { erro = "compraId é obrigatório." });

        var compra = await db.ComprasPlanejadas.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (compra is null)
            return JsonSerializer.Serialize(new { erro = "Compra planejada não encontrada." });

        var titulo = input["titulo"]?.GetValue<string>() ?? compra.Titulo;
        var valorEstimado = input["valorEstimado"]?.GetValue<decimal>() ?? compra.ValorEstimado;
        var prioridade = MapearPrioridade(input["prioridade"]?.GetValue<string>()) ?? compra.Prioridade;
        var parcelavel = input["parcelavel"]?.GetValue<bool>() ?? compra.Parcelavel;
        var parcelas = input["quantidadeParcelasDesejada"]?.GetValue<int>() ?? compra.QuantidadeParcelasDesejada;
        DateOnly? dataDesejada = DateOnly.TryParse(input["dataDesejada"]?.GetValue<string>(), out var dd) ? dd : compra.DataDesejada;
        var contaGerencialId = Guid.TryParse(input["contaGerencialId"]?.GetValue<string>(), out var cg) ? cg : compra.ContaGerencialId;
        var pagadorId = Guid.TryParse(input["pagadorId"]?.GetValue<string>(), out var rp) ? rp : compra.PagadorId;

        compra.Atualizar(
            titulo,
            input["descricao"]?.GetValue<string>() ?? compra.Descricao,
            valorEstimado, dataDesejada, prioridade,
            compra.Status, parcelavel, parcelas,
            contaGerencialId, pagadorId,
            input["link"]?.GetValue<string>() ?? compra.Link,
            input["observacao"]?.GetValue<string>() ?? compra.Observacao);

        await db.SaveChangesAsync(ct);

        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            mensagem = $"Compra planejada '{titulo}' atualizada com sucesso."
        });
    }

    private async Task<string> AlterarStatusAsync(JsonNode input, string acao, CancellationToken ct)
    {
        if (!Guid.TryParse(input["compraId"]?.GetValue<string>(), out var id))
            return JsonSerializer.Serialize(new { erro = "compraId é obrigatório." });

        var compra = await db.ComprasPlanejadas.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (compra is null)
            return JsonSerializer.Serialize(new { erro = "Compra planejada não encontrada." });

        var mensagem = acao switch
        {
            "comprar" => MarcarComoComprada(compra),
            "cancelar" => Cancelar(compra),
            "reverter" => Reverter(compra),
            _ => throw new ArgumentException("Ação inválida.")
        };

        await db.SaveChangesAsync(ct);
        return JsonSerializer.Serialize(new { sucesso = true, mensagem });
    }

    private static string MarcarComoComprada(PlanejamentoCompra compra)
    {
        compra.MarcarComoComprada();
        return $"Compra '{compra.Titulo}' marcada como comprada.";
    }

    private static string Cancelar(PlanejamentoCompra compra)
    {
        compra.CancelarPlanejamento();
        return $"Compra '{compra.Titulo}' cancelada.";
    }

    private static string Reverter(PlanejamentoCompra compra)
    {
        compra.ReverterParaPlanejada();
        return $"Compra '{compra.Titulo}' revertida para planejada.";
    }

    private static PrioridadePlanejamentoCompra? MapearPrioridade(string? p) => p switch
    {
        "baixa" => PrioridadePlanejamentoCompra.Baixa,
        "media" => PrioridadePlanejamentoCompra.Media,
        "alta" => PrioridadePlanejamentoCompra.Alta,
        _ => null
    };
}
