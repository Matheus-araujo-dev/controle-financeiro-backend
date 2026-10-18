using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Financeiro.Importacao;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class ImportarFaturaCartaoTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "importar_fatura_cartao";
    public string Description => "Gera preview de importação de fatura de cartão de crédito. Recebe os lançamentos extraídos de uma fatura (PDF ou digitados) e identifica quais já foram importados e quais são novos. Use quando o usuário enviar uma fatura ou pedir para importar lançamentos de cartão.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "cartaoNome": { "type": "string", "description": "Nome do cartão (ex: 'Mercado Pago', 'Nubank'). Busca parcial, case-insensitive." },
            "dataVencimentoFatura": { "type": "string", "description": "Data de vencimento da fatura no formato DD/MM/YYYY" },
            "itens": {
              "type": "array",
              "description": "Lançamentos extraídos da fatura",
              "items": {
                "type": "object",
                "properties": {
                  "dataTransacao": { "type": "string", "description": "Data da transação DD/MM/YYYY" },
                  "descricao": { "type": "string", "description": "Descrição do lançamento" },
                  "valor": { "type": "number", "description": "Valor em reais (positivo)" },
                  "numeroParcela": { "type": "integer", "description": "Número da parcela atual (padrão 1)" },
                  "quantidadeParcelas": { "type": "integer", "description": "Total de parcelas (padrão 1)" }
                },
                "required": ["dataTransacao", "descricao", "valor"]
              }
            }
          },
          "required": ["cartaoNome", "dataVencimentoFatura", "itens"]
        }
        """;

    private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly string[] DateFormats = ["dd/MM/yyyy", "dd/MM/yy", "d/M/yyyy"];

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        var input = JsonNode.Parse(inputJson);
        var cartaoNome = input?["cartaoNome"]?.GetValue<string>();
        var vencimentoStr = input?["dataVencimentoFatura"]?.GetValue<string>();
        var itensNode = input?["itens"]?.AsArray();

        if (string.IsNullOrWhiteSpace(cartaoNome))
            return JsonSerializer.Serialize(new { erro = "cartaoNome é obrigatório." });

        if (!DateOnly.TryParseExact(vencimentoStr, DateFormats, Pt, DateTimeStyles.None, out var vencimento))
            return JsonSerializer.Serialize(new { erro = "dataVencimentoFatura inválida. Use DD/MM/YYYY." });

        if (itensNode is null || itensNode.Count == 0)
            return JsonSerializer.Serialize(new { erro = "Nenhum item informado." });

        var cartoes = await db.Cartoes
            .AsNoTracking()
            .Where(c => c.Ativo)
            .Select(c => new { c.Id, c.Nome, c.Bandeira, c.NumeroFinal })
            .ToListAsync(cancellationToken);

        var cartao = cartoes.FirstOrDefault(c => c.Nome.Contains(cartaoNome, StringComparison.OrdinalIgnoreCase))
            ?? cartoes.FirstOrDefault(c => cartaoNome.Contains(c.Nome, StringComparison.OrdinalIgnoreCase));

        if (cartao is null)
        {
            var disponiveis = cartoes.Select(c => $"{c.Nome} ({c.Bandeira} ****{c.NumeroFinal})").ToList();
            return JsonSerializer.Serialize(new { erro = $"Cartão '{cartaoNome}' não encontrado.", cartoesDisponiveis = disponiveis });
        }

        var faturaItens = new List<CsvFaturaItem>();
        foreach (var node in itensNode)
        {
            var dataStr = node?["dataTransacao"]?.GetValue<string>();
            var descricao = node?["descricao"]?.GetValue<string>();
            var valor = node?["valor"]?.GetValue<decimal>() ?? 0;
            var numParcela = node?["numeroParcela"]?.GetValue<int>() ?? 1;
            var qtdParcelas = node?["quantidadeParcelas"]?.GetValue<int>() ?? 1;

            if (!DateOnly.TryParseExact(dataStr, DateFormats, Pt, DateTimeStyles.None, out var dataTransacao))
                continue;
            if (string.IsNullOrWhiteSpace(descricao) || valor <= 0)
                continue;

            faturaItens.Add(new CsvFaturaItem(dataTransacao, descricao, valor, vencimento, numParcela, qtdParcelas));
        }

        if (faturaItens.Count == 0)
            return JsonSerializer.Serialize(new { erro = "Nenhum item válido após validação de datas e valores." });

        var occurrences = new Dictionary<string, int>();
        var keyedItems = faturaItens.Select(item =>
        {
            var key = CsvFaturaParser.GerarChave(item);
            occurrences.TryGetValue(key, out var count);
            occurrences[key] = ++count;
            if (count > 1) key += $"-{count}";
            return (Item: item, Key: key);
        }).ToArray();

        var chaves = keyedItems
            .Select(i => $"{cartao.Id}|{i.Key}")
            .ToHashSet();

        var jaImportadas = await db.ContasPagar
            .AsNoTracking()
            .Where(cp => cp.CartaoId == cartao.Id
                      && cp.ChaveSerieImportacaoCartao != null
                      && chaves.Contains(cp.ChaveSerieImportacaoCartao))
            .Select(cp => cp.ChaveSerieImportacaoCartao!)
            .ToListAsync(cancellationToken);

        var jaImportadasSet = jaImportadas.ToHashSet();

        var preview = keyedItems.Select(entry =>
        {
            var i = entry.Item;
            var chaveCompleta = $"{cartao.Id}|{entry.Key}";
            return new
            {
                dataTransacao = i.DataTransacao.ToString("dd/MM/yyyy"),
                descricao = i.Descricao,
                valor = i.Valor,
                parcela = i.QuantidadeParcelas > 1 ? $"{i.NumeroParcela}/{i.QuantidadeParcelas}" : (string?)null,
                jaImportado = jaImportadasSet.Contains(chaveCompleta),
                chaveImportacao = entry.Key
            };
        }).ToList();

        var novos = preview.Where(p => !p.jaImportado).ToList();
        var existentes = preview.Where(p => p.jaImportado).ToList();

        return JsonSerializer.Serialize(new
        {
            cartao = new { cartao.Id, cartao.Nome, cartao.Bandeira, cartao.NumeroFinal },
            vencimentoFatura = vencimento.ToString("dd/MM/yyyy"),
            totalItens = preview.Count,
            novos = novos.Count,
            jaImportados = existentes.Count,
            valorNovos = novos.Sum(n => n.valor),
            valorJaImportados = existentes.Sum(e => e.valor),
            itens = preview
        });
    }
}
