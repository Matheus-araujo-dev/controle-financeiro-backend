using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Exceptions;
using ControleFinanceiro.Application.Financeiro.Reembolsos;
using ControleFinanceiro.Contracts.Financeiro.Common;
using ControleFinanceiro.Contracts.Financeiro.Reembolsos;
using ControleFinanceiro.Domain.Identidade;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class CriarReembolsoTool(IReembolsoAppService reembolsoService) : IFinanceTool
{
    public string Name => "criar_reembolso";
    public string Description => "Cria reembolso a partir de uma conta a pagar existente. Gera contas a receber para os pagadores responsáveis. Quando parcelarIgual=true, distribui o reembolso nas mesmas datas das parcelas originais.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "contaOrigemId":     { "type": "string",  "description": "ID (GUID) de qualquer parcela do grupo de contas a pagar de origem" },
            "parcelarIgual":     { "type": "boolean", "description": "Se true, distribui nas mesmas datas das parcelas originais. Se false, usa dataVencimento informado." },
            "valorTotal":        { "type": "number",  "description": "Valor total do reembolso em reais (pode diferir do valor original)" },
            "pagadoresIds":      { "type": "array", "items": { "type": "string" }, "description": "IDs (GUIDs) dos responsáveis que pagarão o reembolso" },
            "formaPagamentoId":  { "type": "string",  "description": "ID (GUID) da forma de pagamento" },
            "dataVencimento":    { "type": "string",  "description": "Data de vencimento yyyy-MM-dd (usado apenas quando parcelarIgual=false)" },
            "descricao":         { "type": "string",  "description": "Descrição do reembolso" },
            "observacao":        { "type": "string",  "description": "Observação opcional" },
            "rateios":           { "type": "array", "items": { "type": "object", "properties": { "contaGerencialId": { "type": "string" }, "valor": { "type": "number" } } }, "description": "Rateio por conta gerencial de receita" }
          },
          "required": ["contaOrigemId", "valorTotal", "pagadoresIds", "formaPagamentoId", "descricao", "rateios"]
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        if (context.Papel != PapelFamilia.Administrador)
            return JsonSerializer.Serialize(new { erro = "Apenas administradores podem criar reembolsos." });

        var input = JsonNode.Parse(inputJson);

        if (!Guid.TryParse(input?["contaOrigemId"]?.GetValue<string>(), out var contaOrigemId))
            return JsonSerializer.Serialize(new { erro = "contaOrigemId é obrigatório e deve ser um GUID válido." });

        var valorTotal = input?["valorTotal"]?.GetValue<decimal>() ?? 0;
        if (valorTotal <= 0)
            return JsonSerializer.Serialize(new { erro = "valorTotal deve ser maior que zero." });

        var parcelarIgual = input?["parcelarIgual"]?.GetValue<bool>() ?? false;

        var pagadoresNode = input?["pagadoresIds"]?.AsArray();
        if (pagadoresNode is null || pagadoresNode.Count == 0)
            return JsonSerializer.Serialize(new { erro = "Informe ao menos um pagador em pagadoresIds." });

        var pagadoresIds = new List<Guid>();
        foreach (var p in pagadoresNode)
        {
            if (Guid.TryParse(p?.GetValue<string>(), out var pid))
                pagadoresIds.Add(pid);
            else
                return JsonSerializer.Serialize(new { erro = $"ID de pagador inválido: {p}" });
        }

        if (!Guid.TryParse(input?["formaPagamentoId"]?.GetValue<string>(), out var formaPagamentoId))
            return JsonSerializer.Serialize(new { erro = "formaPagamentoId é obrigatório e deve ser um GUID válido." });

        var descricao = input?["descricao"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(descricao))
            return JsonSerializer.Serialize(new { erro = "descricao é obrigatória." });

        var observacao = input?["observacao"]?.GetValue<string>();

        var dataVencStr = input?["dataVencimento"]?.GetValue<string>();
        DateOnly dataVencimento = default;
        if (!parcelarIgual)
        {
            if (string.IsNullOrWhiteSpace(dataVencStr) || !DateOnly.TryParse(dataVencStr, out dataVencimento))
                return JsonSerializer.Serialize(new { erro = "dataVencimento é obrigatório quando parcelarIgual=false (formato yyyy-MM-dd)." });
        }

        var rateiosNode = input?["rateios"]?.AsArray();
        if (rateiosNode is null || rateiosNode.Count == 0)
            return JsonSerializer.Serialize(new { erro = "Informe ao menos um rateio." });

        var rateios = new List<RateioRequest>();
        foreach (var r in rateiosNode)
        {
            if (!Guid.TryParse(r?["contaGerencialId"]?.GetValue<string>(), out var cgId))
                return JsonSerializer.Serialize(new { erro = "contaGerencialId inválido em rateio." });
            var valor = r?["valor"]?.GetValue<decimal>() ?? 0;
            if (valor <= 0)
                return JsonSerializer.Serialize(new { erro = "Valor do rateio deve ser maior que zero." });
            rateios.Add(new RateioRequest(cgId, valor));
        }

        var request = new CriarReembolsoContaPagarRequest(
            contaOrigemId,
            parcelarIgual,
            valorTotal,
            pagadoresIds,
            formaPagamentoId,
            dataVencimento,
            descricao,
            observacao,
            rateios);

        try
        {
            var resultado = await reembolsoService.CriarReembolsoContaPagarAsync(request, cancellationToken);

            return JsonSerializer.Serialize(new
            {
                sucesso = true,
                grupoReembolsoId = resultado.GrupoReembolsoId,
                totalContasGeradas = resultado.ContasReceber.Count,
                contasReceber = resultado.ContasReceber.Select(c => new
                {
                    id = c.Id,
                    pagador = c.PagadorNome,
                    parcela = $"{c.NumeroParcela}/{c.QuantidadeParcelas}",
                    valor = c.ValorLiquido,
                    vencimento = c.DataVencimento.ToString("yyyy-MM-dd"),
                    descricao = c.Descricao
                }),
                mensagem = $"Reembolso criado com sucesso: {resultado.ContasReceber.Count} conta(s) a receber gerada(s), valor total R$ {valorTotal:N2}."
            });
        }
        catch (ApplicationValidationException ex)
        {
            var erros = ex.Errors.SelectMany(e => e.Value).ToList();
            return JsonSerializer.Serialize(new { erro = string.Join(" ", erros) });
        }
        catch (InvalidOperationException ex)
        {
            return JsonSerializer.Serialize(new { erro = ex.Message });
        }
    }
}
