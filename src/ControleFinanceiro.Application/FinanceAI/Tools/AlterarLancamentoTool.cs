using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Financeiro;
using ControleFinanceiro.Domain.Identidade;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class AlterarLancamentoTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "alterar_lancamento";
    public string Description => "Altera uma conta a pagar ou conta a receber existente. Informe o ID e o tipo (pagar/receber), mais os campos a alterar. Não é possível alterar contas liquidadas ou canceladas. Chame apenas após confirmação do usuário.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "id":                { "type": "string",  "description": "ID do lançamento a alterar" },
            "tipo":              { "type": "string",  "enum": ["pagar", "receber"], "description": "Tipo: pagar ou receber" },
            "descricao":         { "type": "string",  "description": "Nova descrição (opcional)" },
            "valor":             { "type": "number",  "description": "Novo valor em reais (opcional)" },
            "dataVencimento":    { "type": "string",  "description": "Nova data de vencimento YYYY-MM-DD (opcional)" },
            "contaGerencialId":  { "type": "string",  "description": "Novo ID da categoria (opcional)" },
            "observacao":        { "type": "string",  "description": "Nova observação (opcional)" }
          },
          "required": ["id", "tipo"]
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        if (context.Papel == PapelFamilia.Visualizador)
            return JsonSerializer.Serialize(new { erro = "Visualizadores não podem alterar lançamentos." });

        var input = JsonNode.Parse(inputJson);
        var idStr = input?["id"]?.GetValue<string>();
        var tipo = input?["tipo"]?.GetValue<string>();

        if (!Guid.TryParse(idStr, out var id))
            return JsonSerializer.Serialize(new { erro = "ID inválido." });

        if (tipo != "pagar" && tipo != "receber")
            return JsonSerializer.Serialize(new { erro = "Tipo deve ser 'pagar' ou 'receber'." });

        var descricao = input?["descricao"]?.GetValue<string>();
        var valorNum = input?["valor"];
        decimal? valor = valorNum is not null ? valorNum.GetValue<decimal>() : null;
        var dataVencimentoStr = input?["dataVencimento"]?.GetValue<string>();
        var contaGerencialIdStr = input?["contaGerencialId"]?.GetValue<string>();
        var observacao = input?["observacao"]?.GetValue<string>();

        try
        {
            if (tipo == "pagar")
                return await AlterarContaPagarAsync(id, descricao, valor, dataVencimentoStr, contaGerencialIdStr, observacao, cancellationToken);
            else
                return await AlterarContaReceberAsync(id, descricao, valor, dataVencimentoStr, contaGerencialIdStr, observacao, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return JsonSerializer.Serialize(new { erro = ex.Message });
        }
    }

    private async Task<string> AlterarContaPagarAsync(
        Guid id, string? descricao, decimal? valor, string? dataVencimentoStr,
        string? contaGerencialIdStr, string? observacao, CancellationToken ct)
    {
        var conta = await db.ContasPagar
            .Include(c => c.Rateios)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (conta is null)
            return JsonSerializer.Serialize(new { erro = "Conta a pagar não encontrada." });

        var novaDescricao = descricao ?? conta.Descricao;
        var novoValor = valor ?? conta.ValorOriginal;
        var novaData = DateOnly.TryParse(dataVencimentoStr, out var dv) ? dv : conta.DataVencimento;
        var novaObs = observacao ?? conta.Observacao;

        Guid contaGerencialId;
        if (Guid.TryParse(contaGerencialIdStr, out var cgId))
            contaGerencialId = cgId;
        else if (conta.Rateios.Count > 0)
            contaGerencialId = conta.Rateios.First().ContaGerencialId;
        else
            return JsonSerializer.Serialize(new { erro = "contaGerencialId é obrigatório quando a conta não possui rateio." });

        var rateio = RateioPlano.Create(contaGerencialId, novoValor);

        conta.Atualizar(
            numeroDocumento: conta.NumeroDocumento,
            dataEmissao: conta.DataEmissao,
            responsavelCompraId: conta.ResponsavelCompraId,
            recebedorId: conta.RecebedorId,
            dataVencimento: novaData,
            formaPagamentoId: conta.FormaPagamentoId,
            cartaoId: conta.CartaoId,
            contaBancariaId: conta.ContaBancariaId,
            valorOriginal: novoValor,
            valorDesconto: conta.ValorDesconto,
            valorJuros: conta.ValorJuros,
            valorMulta: conta.ValorMulta,
            descricao: novaDescricao,
            observacao: novaObs,
            statusContaId: conta.StatusContaId,
            rateios: [rateio]);

        await db.SaveChangesAsync(ct);

        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            mensagem = $"Conta a pagar alterada: {novaDescricao} - R$ {novoValor:N2} vence em {novaData:dd/MM/yyyy}."
        });
    }

    private async Task<string> AlterarContaReceberAsync(
        Guid id, string? descricao, decimal? valor, string? dataVencimentoStr,
        string? contaGerencialIdStr, string? observacao, CancellationToken ct)
    {
        var conta = await db.ContasReceber
            .Include(c => c.Rateios)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (conta is null)
            return JsonSerializer.Serialize(new { erro = "Conta a receber não encontrada." });

        var novaDescricao = descricao ?? conta.Descricao;
        var novoValor = valor ?? conta.ValorOriginal;
        var novaData = DateOnly.TryParse(dataVencimentoStr, out var dv) ? dv : conta.DataVencimento;
        var novaObs = observacao ?? conta.Observacao;

        Guid contaGerencialId;
        if (Guid.TryParse(contaGerencialIdStr, out var cgId))
            contaGerencialId = cgId;
        else if (conta.Rateios.Count > 0)
            contaGerencialId = conta.Rateios.First().ContaGerencialId;
        else
            return JsonSerializer.Serialize(new { erro = "contaGerencialId é obrigatório quando a conta não possui rateio." });

        var rateio = RateioPlano.Create(contaGerencialId, novoValor);

        conta.Atualizar(
            numeroDocumento: conta.NumeroDocumento,
            dataEmissao: conta.DataEmissao,
            responsavelId: conta.ResponsavelId,
            pagadorId: conta.PagadorId,
            dataVencimento: novaData,
            formaPagamentoId: conta.FormaPagamentoId,
            cartaoId: conta.CartaoId,
            contaBancariaId: conta.ContaBancariaId,
            valorOriginal: novoValor,
            valorDesconto: conta.ValorDesconto,
            valorJuros: conta.ValorJuros,
            valorMulta: conta.ValorMulta,
            descricao: novaDescricao,
            observacao: novaObs,
            statusContaId: conta.StatusContaId,
            rateios: [rateio]);

        await db.SaveChangesAsync(ct);

        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            mensagem = $"Conta a receber alterada: {novaDescricao} - R$ {novoValor:N2} vence em {novaData:dd/MM/yyyy}."
        });
    }
}
