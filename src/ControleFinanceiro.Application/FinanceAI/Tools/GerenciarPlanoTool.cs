using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Financeiro;
using ControleFinanceiro.Domain.Identidade;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class GerenciarPlanoTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "gerenciar_plano";
    public string Description => "Gerencia planos de economia: criar plano, adiantar parcela, retirar dinheiro ou cancelar. Para consultar planos, use buscar_planos.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "acao":              { "type": "string",  "enum": ["criar", "adiantar_parcela", "retirar", "cancelar"], "description": "Ação a executar" },
            "planoId":           { "type": "string",  "description": "ID do plano (obrigatório para adiantar_parcela, retirar e cancelar)" },
            "nome":              { "type": "string",  "description": "Nome do plano (criar)" },
            "descricao":         { "type": "string",  "description": "Descrição do plano (criar, opcional)" },
            "valorMensal":       { "type": "number",  "description": "Valor mensal a poupar em reais (criar)" },
            "numParcelas":       { "type": "integer", "description": "Número total de parcelas/meses (criar)" },
            "contaBancariaId":   { "type": "string",  "description": "ID da conta bancária caixa do plano (criar)" },
            "valor":             { "type": "number",  "description": "Valor a retirar do plano (retirar)" }
          },
          "required": ["acao"]
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        if (context.Papel == PapelFamilia.Visualizador)
            return JsonSerializer.Serialize(new { erro = "Visualizadores não podem gerenciar planos." });

        var input = JsonNode.Parse(inputJson);
        var acao = input?["acao"]?.GetValue<string>();

        try
        {
            return acao switch
            {
                "criar" => await CriarAsync(input!, cancellationToken),
                "adiantar_parcela" => await AdiantarParcelaAsync(input!, cancellationToken),
                "retirar" => await RetirarAsync(input!, cancellationToken),
                "cancelar" => await CancelarAsync(input!, cancellationToken),
                _ => JsonSerializer.Serialize(new { erro = "Ação inválida. Use: criar, adiantar_parcela, retirar ou cancelar." })
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

    private async Task<string> CriarAsync(JsonNode input, CancellationToken ct)
    {
        var nome = input["nome"]?.GetValue<string>();
        var descricao = input["descricao"]?.GetValue<string>();
        var valorMensal = input["valorMensal"]?.GetValue<decimal>() ?? 0;
        var numParcelas = input["numParcelas"]?.GetValue<int>() ?? 0;
        var contaBancariaIdStr = input["contaBancariaId"]?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(nome))
            return JsonSerializer.Serialize(new { erro = "Nome do plano é obrigatório." });

        if (valorMensal <= 0)
            return JsonSerializer.Serialize(new { erro = "Valor mensal deve ser maior que zero." });

        if (numParcelas <= 0)
            return JsonSerializer.Serialize(new { erro = "Número de parcelas deve ser maior que zero." });

        if (!Guid.TryParse(contaBancariaIdStr, out var contaBancariaId))
            return JsonSerializer.Serialize(new { erro = "contaBancariaId é obrigatório e deve ser um ID válido." });

        var contaExiste = await db.ContasBancarias.AsNoTracking()
            .AnyAsync(c => c.Id == contaBancariaId, ct);
        if (!contaExiste)
            return JsonSerializer.Serialize(new { erro = "Conta bancária não encontrada." });

        var plano = Plano.Criar(nome, descricao, valorMensal, numParcelas, contaBancariaId);

        db.Planos.Add(plano);
        await db.SaveChangesAsync(ct);

        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            planoId = plano.Id,
            mensagem = $"Plano '{nome}' criado: R$ {valorMensal:N2}/mês x {numParcelas} parcelas = R$ {plano.ValorTotal:N2} total."
        });
    }

    private async Task<string> AdiantarParcelaAsync(JsonNode input, CancellationToken ct)
    {
        if (!Guid.TryParse(input["planoId"]?.GetValue<string>(), out var id))
            return JsonSerializer.Serialize(new { erro = "planoId é obrigatório." });

        var plano = await db.Planos
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (plano is null)
            return JsonSerializer.Serialize(new { erro = "Plano não encontrado." });

        plano.AdiantarParcela();
        await db.SaveChangesAsync(ct);

        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            mensagem = $"Parcela adiantada. Progresso: {plano.ParcelasPagas}/{plano.NumParcelas}. Acumulado: R$ {plano.TotalAcumulado:N2}."
        });
    }

    private async Task<string> RetirarAsync(JsonNode input, CancellationToken ct)
    {
        if (!Guid.TryParse(input["planoId"]?.GetValue<string>(), out var id))
            return JsonSerializer.Serialize(new { erro = "planoId é obrigatório." });

        var valor = input["valor"]?.GetValue<decimal>();
        if (valor is null || valor <= 0)
            return JsonSerializer.Serialize(new { erro = "Valor de retirada deve ser maior que zero." });

        var plano = await db.Planos
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (plano is null)
            return JsonSerializer.Serialize(new { erro = "Plano não encontrado." });

        plano.RetirarDinheiro(valor.Value);
        await db.SaveChangesAsync(ct);

        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            mensagem = $"Retirada de R$ {valor.Value:N2} realizada. Saldo acumulado restante: R$ {plano.TotalAcumulado:N2}."
        });
    }

    private async Task<string> CancelarAsync(JsonNode input, CancellationToken ct)
    {
        if (!Guid.TryParse(input["planoId"]?.GetValue<string>(), out var id))
            return JsonSerializer.Serialize(new { erro = "planoId é obrigatório." });

        var plano = await db.Planos
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (plano is null)
            return JsonSerializer.Serialize(new { erro = "Plano não encontrado." });

        plano.Cancelar();
        await db.SaveChangesAsync(ct);

        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            mensagem = $"Plano '{plano.Nome}' cancelado. Acumulado final: R$ {plano.TotalAcumulado:N2}."
        });
    }
}
