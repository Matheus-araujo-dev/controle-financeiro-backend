using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Financeiro.Importacao;
using ControleFinanceiro.Domain.Cadastros.Pessoas;
using ControleFinanceiro.Domain.Financeiro;
using ControleFinanceiro.Domain.Identidade;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class ConfirmarImportacaoFaturaCartaoTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "confirmar_importacao_fatura";
    public string Description => "Confirma a importação de lançamentos novos de uma fatura de cartão. Use APÓS o preview (importar_fatura_cartao) e APÓS o usuário confirmar, informando recebedor e conta gerencial. Apenas Administradores podem confirmar.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "cartaoNome": { "type": "string", "description": "Nome do cartão (ex: 'Mercado Pago')" },
            "dataVencimentoFatura": { "type": "string", "description": "Data de vencimento da fatura DD/MM/YYYY" },
            "recebedorNome": { "type": "string", "description": "Nome do recebedor padrão (fornecedor). Se não existir, será criado." },
            "contaGerencialNome": { "type": "string", "description": "Nome (descrição) da conta gerencial padrão para os lançamentos." },
            "itens": {
              "type": "array",
              "description": "Lançamentos a importar (apenas os novos, não os já importados)",
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
          "required": ["cartaoNome", "dataVencimentoFatura", "recebedorNome", "contaGerencialNome", "itens"]
        }
        """;

    private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly string[] DateFormats = ["dd/MM/yyyy", "dd/MM/yy", "d/M/yyyy"];

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        if (context.Papel != PapelFamilia.Administrador)
            return JsonSerializer.Serialize(new { erro = "Apenas administradores podem confirmar importações." });

        var input = JsonNode.Parse(inputJson);
        var cartaoNome = input?["cartaoNome"]?.GetValue<string>();
        var vencimentoStr = input?["dataVencimentoFatura"]?.GetValue<string>();
        var recebedorNome = input?["recebedorNome"]?.GetValue<string>();
        var contaGerencialNome = input?["contaGerencialNome"]?.GetValue<string>();
        var itensNode = input?["itens"]?.AsArray();

        if (string.IsNullOrWhiteSpace(cartaoNome))
            return JsonSerializer.Serialize(new { erro = "cartaoNome é obrigatório." });
        if (!DateOnly.TryParseExact(vencimentoStr, DateFormats, Pt, DateTimeStyles.None, out var vencimento))
            return JsonSerializer.Serialize(new { erro = "dataVencimentoFatura inválida. Use DD/MM/YYYY." });
        if (string.IsNullOrWhiteSpace(recebedorNome))
            return JsonSerializer.Serialize(new { erro = "recebedorNome é obrigatório." });
        if (string.IsNullOrWhiteSpace(contaGerencialNome))
            return JsonSerializer.Serialize(new { erro = "contaGerencialNome é obrigatório." });
        if (itensNode is null || itensNode.Count == 0)
            return JsonSerializer.Serialize(new { erro = "Nenhum item informado para importar." });

        var cartao = await db.Cartoes.AsNoTracking()
            .Where(c => c.Ativo)
            .Select(c => new { c.Id, c.Nome })
            .ToListAsync(cancellationToken)
            .ContinueWith(t => t.Result
                .FirstOrDefault(c => c.Nome.Contains(cartaoNome, StringComparison.OrdinalIgnoreCase))
                ?? t.Result.FirstOrDefault(c => cartaoNome.Contains(c.Nome, StringComparison.OrdinalIgnoreCase)),
                cancellationToken);

        if (cartao is null)
            return JsonSerializer.Serialize(new { erro = $"Cartão '{cartaoNome}' não encontrado." });

        var recebedorId = await ResolverOuCriarRecebedorAsync(recebedorNome, context.FamiliaId, cancellationToken);

        var contaGerencialNomeLower = contaGerencialNome.ToLower();
        var contaGerencialId = await db.ContasGerenciais.AsNoTracking()
            .Where(c => c.Ativo && EF.Functions.Like(c.Descricao.ToLower(), $"%{contaGerencialNomeLower}%"))
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (contaGerencialId is null)
        {
            var disponiveis = await db.ContasGerenciais.AsNoTracking()
                .Where(c => c.Ativo && !db.ContasGerenciais.Any(child => child.ContaPaiId == c.Id))
                .Select(c => c.Descricao)
                .Take(20)
                .ToListAsync(cancellationToken);
            return JsonSerializer.Serialize(new { erro = $"Conta gerencial '{contaGerencialNome}' não encontrada.", contasDisponiveis = disponiveis });
        }

        var formaPagamentoId = await db.FormasPagamento.AsNoTracking()
            .Where(f => f.Ativo && f.EhCartao)
            .Select(f => (Guid?)f.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (formaPagamentoId is null)
        {
            formaPagamentoId = await db.FormasPagamento.AsNoTracking()
                .Where(f => f.Ativo)
                .OrderBy(f => f.Tipo)
                .Select(f => (Guid?)f.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (formaPagamentoId is null)
            return JsonSerializer.Serialize(new { erro = "Nenhuma forma de pagamento encontrada." });

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
            return JsonSerializer.Serialize(new { erro = "Nenhum item válido após validação." });

        var occurrences = new Dictionary<string, int>();
        var keyedItems = faturaItens.Select(item =>
        {
            var key = CsvFaturaParser.GerarChave(item);
            occurrences.TryGetValue(key, out var count);
            occurrences[key] = ++count;
            if (count > 1) key += $"-{count}";
            return (Item: item, Key: key);
        }).ToArray();

        var chavesCompletas = keyedItems.Select(i => $"{cartao.Id}|{i.Key}").ToHashSet();

        var existentes = (await db.ContasPagar.AsNoTracking()
            .Where(cp => cp.CartaoId == cartao.Id
                      && cp.ChaveSerieImportacaoCartao != null
                      && chavesCompletas.Contains(cp.ChaveSerieImportacaoCartao))
            .Select(cp => cp.ChaveSerieImportacaoCartao!)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var criadas = 0;
        var duplicadas = 0;

        foreach (var entry in keyedItems)
        {
            var chaveCompleta = $"{cartao.Id}|{entry.Key}";
            if (existentes.Contains(chaveCompleta))
            {
                duplicadas++;
                continue;
            }

            var item = entry.Item;
            var rateio = RateioPlano.CreateSigned(contaGerencialId.Value, item.Valor);
            var conta = ContaPagar.Criar(
                numeroDocumento: null,
                dataEmissao: item.DataTransacao,
                pagadorId: null,
                recebedorId: recebedorId,
                dataVencimento: vencimento,
                formaPagamentoId: formaPagamentoId.Value,
                cartaoId: cartao.Id,
                contaBancariaId: null,
                valorOriginal: item.Valor,
                valorDesconto: 0m,
                valorJuros: 0m,
                valorMulta: 0m,
                quantidadeParcelas: item.QuantidadeParcelas,
                numeroParcela: item.NumeroParcela,
                grupoParcelamentoId: null,
                origemCompraPlanejadaId: null,
                descricao: item.Descricao,
                observacao: "Importação via agente IA",
                statusContaId: StatusConta.EmFaturaId,
                ehRecorrente: false,
                regraRecorrenciaId: null,
                origem: OrigemLancamento.Importacao,
                rateios: [rateio],
                dataCompra: item.DataTransacao);

            conta.DefinirChaveSerieImportacaoCartao(chaveCompleta);
            db.ContasPagar.Add(conta);
            db.RateiosContaGerencial.AddRange(conta.Rateios);
            existentes.Add(chaveCompleta);
            criadas++;
        }

        if (criadas > 0)
            await db.SaveChangesAsync(cancellationToken);

        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            criadas,
            duplicadas,
            mensagem = $"Importação concluída: {criadas} lançamentos criados no cartão {cartao.Nome}, vencimento {vencimento:dd/MM/yyyy}."
                + (duplicadas > 0 ? $" {duplicadas} itens já existiam e foram ignorados." : "")
        });
    }

    private async Task<Guid> ResolverOuCriarRecebedorAsync(string nome, Guid familiaId, CancellationToken ct)
    {
        var nomeLower = nome.Trim().ToLower();
        var existente = await db.Pessoas.AsNoTracking()
            .Where(p => p.Ativo && EF.Functions.Like(p.Nome.ToLower(), nomeLower))
            .Select(p => p.Id)
            .FirstOrDefaultAsync(ct);

        if (existente != Guid.Empty)
            return existente;

        var nova = Pessoa.Criar(
            nome: nome.Trim(),
            tipoPessoa: TipoPessoa.Juridica,
            cpfCnpj: null,
            email: null,
            telefone: null,
            observacao: "Criado automaticamente pela importação via agente IA",
            chavesPix: [],
            ativo: true);
        nova.AtribuirFamilia(familiaId);
        db.Pessoas.Add(nova);
        return nova.Id;
    }
}
