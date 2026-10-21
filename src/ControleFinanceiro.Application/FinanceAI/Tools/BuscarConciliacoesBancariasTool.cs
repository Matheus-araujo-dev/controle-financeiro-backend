using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Conciliacao;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class BuscarConciliacoesBancariasTool(ConciliacaoAppService conciliacao) : IFinanceTool
{
    public string Name => "buscar_conciliacoes_bancarias";
    public string Description => "Consulta conciliações bancárias. Sem parâmetros retorna lista das recentes; com conciliacaoId retorna detalhe com itens e sugestões de match.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "conciliacaoId": { "type": "string", "description": "ID da conciliação (GUID). Se informado, retorna detalhe com itens." }
          },
          "required": []
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        var input = JsonNode.Parse(inputJson);
        var conciliacaoIdStr = input?["conciliacaoId"]?.GetValue<string>();

        if (!string.IsNullOrWhiteSpace(conciliacaoIdStr) && Guid.TryParse(conciliacaoIdStr, out var conciliacaoId))
        {
            var detalhe = await conciliacao.ObterAsync(conciliacaoId, cancellationToken);
            if (detalhe is null)
                return JsonSerializer.Serialize(new { erro = "Conciliação bancária não encontrada." });

            var pendentes = detalhe.Itens.Where(i => i.Status == "Pendente").ToArray();
            var conciliados = detalhe.Itens.Where(i => i.Status == "Conciliado").ToArray();

            return JsonSerializer.Serialize(new
            {
                id = detalhe.Id,
                arquivo = detalhe.NomeArquivo,
                formato = detalhe.Formato,
                contaBancariaId = detalhe.ContaBancariaId,
                periodo = $"{detalhe.DataInicio:yyyy-MM-dd} a {detalhe.DataFim:yyyy-MM-dd}",
                status = detalhe.Status,
                totalItens = detalhe.TotalItens,
                itensConciliados = detalhe.ItensConciliados,
                itens = detalhe.Itens.Select(i => new
                {
                    id = i.Id,
                    data = i.Data.ToString("yyyy-MM-dd"),
                    descricao = i.Descricao,
                    valor = i.Valor,
                    documento = i.Documento,
                    status = i.Status,
                    movimentacaoVinculadaId = i.MovimentacaoVinculadaId,
                    sugestaoMovimentacaoId = i.SugestaoMovimentacaoId,
                    scoreSugestao = i.ScoreSugestao
                })
            });
        }

        var lista = await conciliacao.ListarAsync(cancellationToken);

        if (lista.Count == 0)
            return JsonSerializer.Serialize(new { total = 0, mensagem = "Nenhuma conciliação bancária encontrada." });

        return JsonSerializer.Serialize(new
        {
            total = lista.Count,
            conciliacoes = lista.Select(c => new
            {
                id = c.Id,
                arquivo = c.NomeArquivo,
                formato = c.Formato,
                contaBancariaId = c.ContaBancariaId,
                periodo = $"{c.DataInicio:yyyy-MM-dd} a {c.DataFim:yyyy-MM-dd}",
                totalItens = c.TotalItens,
                itensConciliados = c.ItensConciliados,
                status = c.Status,
                criadoEm = c.CriadoEmUtc.ToString("yyyy-MM-dd HH:mm")
            })
        });
    }
}
