using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Identidade;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class GerenciarFaturaTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "gerenciar_fatura";
    public string Description => "Gerencia faturas de cartão de crédito: pagar, fechar, estornar pagamento ou reabrir. Apenas administradores. Para consultar faturas, use buscar_faturas_cartao.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "acao":                       { "type": "string",  "enum": ["pagar", "fechar", "estornar", "reabrir"], "description": "Ação: pagar (marca como paga), fechar (fecha fatura aberta), estornar (reverte pagamento), reabrir (reabre fatura fechada)" },
            "faturaId":                   { "type": "string",  "description": "ID da fatura" },
            "dataPagamento":              { "type": "string",  "description": "Data do pagamento YYYY-MM-DD (padrão: hoje, apenas para pagar)" },
            "contaBancariaPagamentoId":   { "type": "string",  "description": "ID da conta bancária usada no pagamento (obrigatório para pagar)" },
            "observacao":                 { "type": "string",  "description": "Observação (opcional, para pagar)" }
          },
          "required": ["acao", "faturaId"]
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        if (context.Papel != PapelFamilia.Administrador)
            return JsonSerializer.Serialize(new { erro = "Apenas administradores podem gerenciar faturas." });

        var input = JsonNode.Parse(inputJson);
        var acao = input?["acao"]?.GetValue<string>();

        if (!Guid.TryParse(input?["faturaId"]?.GetValue<string>(), out var faturaId))
            return JsonSerializer.Serialize(new { erro = "faturaId é obrigatório." });

        var fatura = await db.FaturasCartao.FirstOrDefaultAsync(f => f.Id == faturaId, cancellationToken);
        if (fatura is null)
            return JsonSerializer.Serialize(new { erro = "Fatura não encontrada." });

        try
        {
            var mensagem = acao switch
            {
                "pagar" => await PagarAsync(input!, fatura, cancellationToken),
                "fechar" => Fechar(fatura),
                "estornar" => Estornar(fatura),
                "reabrir" => Reabrir(fatura),
                _ => throw new ArgumentException("Ação inválida. Use: pagar, fechar, estornar ou reabrir.")
            };

            await db.SaveChangesAsync(cancellationToken);
            return JsonSerializer.Serialize(new { sucesso = true, mensagem });
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

    private async Task<string> PagarAsync(JsonNode input, Domain.Financeiro.FaturaCartao fatura, CancellationToken ct)
    {
        if (!Guid.TryParse(input["contaBancariaPagamentoId"]?.GetValue<string>(), out var contaId))
            throw new ArgumentException("contaBancariaPagamentoId é obrigatório para pagar uma fatura.");

        if (!await db.ContasBancarias.AsNoTracking().AnyAsync(c => c.Id == contaId, ct))
            throw new ArgumentException("Conta bancária não encontrada.");

        var dataPagamento = DateOnly.TryParse(input["dataPagamento"]?.GetValue<string>(), out var dp)
            ? dp : DateOnly.FromDateTime(DateTime.UtcNow);
        var observacao = input["observacao"]?.GetValue<string>();

        fatura.Pagar(dataPagamento, contaId, observacao);
        return $"Fatura {fatura.Competencia} paga em {dataPagamento:dd/MM/yyyy}. Valor: R$ {fatura.ValorTotal:N2}.";
    }

    private static string Fechar(Domain.Financeiro.FaturaCartao fatura)
    {
        fatura.Fechar();
        return $"Fatura {fatura.Competencia} fechada. Valor: R$ {fatura.ValorTotal:N2}.";
    }

    private static string Estornar(Domain.Financeiro.FaturaCartao fatura)
    {
        fatura.ReabrirPagamento();
        return $"Pagamento da fatura {fatura.Competencia} estornado. Fatura reaberta.";
    }

    private static string Reabrir(Domain.Financeiro.FaturaCartao fatura)
    {
        fatura.Reabrir();
        return $"Fatura {fatura.Competencia} reaberta.";
    }

}
