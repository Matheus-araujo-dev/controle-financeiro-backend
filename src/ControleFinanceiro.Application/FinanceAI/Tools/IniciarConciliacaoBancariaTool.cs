using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Conciliacao;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class IniciarConciliacaoBancariaTool(IAppDbContext db, ConciliacaoAppService conciliacao) : IFinanceTool
{
    public string Name => "iniciar_conciliacao_bancaria";
    public string Description => "Inicia conciliação bancária a partir de arquivo OFX ou CSV anexado. Faz parse do extrato, identifica movimentações e encontra correspondências inteligentes com lançamentos existentes.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "contaBancariaId":   { "type": "string", "description": "ID da conta bancária (GUID). Se informado, usa diretamente." },
            "contaBancariaNome": { "type": "string", "description": "Nome da conta bancária (busca parcial). Usado quando o ID não é conhecido." }
          },
          "required": []
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        var input = JsonNode.Parse(inputJson);
        var contaIdStr = input?["contaBancariaId"]?.GetValue<string>();
        var contaNome = input?["contaBancariaNome"]?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(context.ArquivoAnexoPath))
            return JsonSerializer.Serialize(new { erro = "Nenhum arquivo foi anexado. Peça ao usuário para enviar o arquivo OFX ou CSV do extrato bancário." });

        if (!File.Exists(context.ArquivoAnexoPath))
            return JsonSerializer.Serialize(new { erro = "Arquivo anexado não encontrado no servidor." });

        Guid? contaBancariaId = null;
        if (!string.IsNullOrWhiteSpace(contaIdStr) && Guid.TryParse(contaIdStr, out var cid))
            contaBancariaId = cid;

        if (contaBancariaId is null)
        {
            if (string.IsNullOrWhiteSpace(contaNome))
                return JsonSerializer.Serialize(new { erro = "Informe contaBancariaId ou contaBancariaNome para identificar a conta." });

            var contas = await db.ContasBancarias.AsNoTracking()
                .Where(c => c.Ativo)
                .Select(c => new { c.Id, c.Nome, c.Banco })
                .ToListAsync(cancellationToken);

            var conta = contas.FirstOrDefault(c => c.Nome.Contains(contaNome, StringComparison.OrdinalIgnoreCase))
                ?? contas.FirstOrDefault(c => contaNome.Contains(c.Nome, StringComparison.OrdinalIgnoreCase));

            if (conta is null)
            {
                var disponiveis = contas.Select(c => $"{c.Nome} ({c.Banco})").ToList();
                return JsonSerializer.Serialize(new { erro = $"Conta bancária '{contaNome}' não encontrada.", contasDisponiveis = disponiveis });
            }

            contaBancariaId = conta.Id;
        }

        var fileName = context.ArquivoAnexoNome ?? "extrato.ofx";
        try
        {
            await using var stream = new FileStream(context.ArquivoAnexoPath, FileMode.Open, FileAccess.Read);
            var resultado = await conciliacao.IniciarAsync(contaBancariaId.Value, fileName, stream, cancellationToken);

            if (resultado is null)
                return JsonSerializer.Serialize(new { erro = "Conta bancária não encontrada." });

            var pendentes = resultado.Itens.Where(i => i.Status == "Pendente").ToArray();
            var conciliados = resultado.Itens.Where(i => i.Status == "Conciliado").ToArray();
            var ignorados = resultado.Itens.Where(i => i.Status == "Ignorado").ToArray();
            var comSugestao = pendentes.Count(i => i.SugestaoMovimentacaoId.HasValue);

            return JsonSerializer.Serialize(new
            {
                conciliacaoId = resultado.Id,
                contaBancariaId = resultado.ContaBancariaId,
                arquivo = resultado.NomeArquivo,
                formato = resultado.Formato,
                periodo = $"{resultado.DataInicio:yyyy-MM-dd} a {resultado.DataFim:yyyy-MM-dd}",
                resumo = new
                {
                    totalItens = resultado.Itens.Count,
                    pendentes = pendentes.Length,
                    conciliados = conciliados.Length,
                    ignorados = ignorados.Length,
                    comSugestao,
                    valorTotalPendente = pendentes.Sum(i => i.Valor),
                    valorTotalConciliado = conciliados.Sum(i => i.Valor)
                },
                instrucao = pendentes.Length > 0
                    ? $"Há {pendentes.Length} itens pendentes ({comSugestao} com sugestão de match). Use buscar_conciliacoes_bancarias com o conciliacaoId para ver os detalhes, e gerenciar_item_conciliacao_bancaria para conciliar ou ignorar cada item."
                    : "Todos os itens já foram processados."
            });
        }
        catch (Exception ex) when (ex.Message.Contains("Formato") || ex.Message.Contains("Arquivo") || ex.Message.Contains("transacao"))
        {
            return JsonSerializer.Serialize(new { erro = ex.Message });
        }
    }
}
