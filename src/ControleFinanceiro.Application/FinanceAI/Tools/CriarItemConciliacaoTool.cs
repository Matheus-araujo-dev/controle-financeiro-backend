using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Conciliacao;
using ControleFinanceiro.Contracts.Conciliacao;
using ControleFinanceiro.Contracts.Financeiro.Common;
using ControleFinanceiro.Domain.Cadastros.Pessoas;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class CriarItemConciliacaoTool(IAppDbContext db, CriarItemFaturaAppService criarItem) : IFinanceTool
{
    public string Name => "criar_item_conciliacao";
    public string Description => "Cria um novo lançamento (conta a pagar) a partir de um item da fatura que não tem correspondência no sistema. Resolve nomes de pessoas e categorias para IDs automaticamente. Aprende preferências para futuras importações.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "faturaId": { "type": "string", "description": "ID da fatura (GUID)" },
            "conciliacaoId": { "type": "string", "description": "ID da sessão de conciliação (GUID)" },
            "itemId": { "type": "string", "description": "ID do item pendente (GUID)" },
            "descricao": { "type": "string", "description": "Descrição para o novo lançamento (ex: 'Mercado Livre - Eletrônicos')" },
            "recebedorNome": { "type": "string", "description": "Nome do recebedor/fornecedor (busca parcial). Se não existir, será criado." },
            "responsavelNome": { "type": "string", "description": "Nome do responsável pela compra (busca parcial). Se omitido, usa o usuário logado." },
            "contaGerencialNome": { "type": "string", "description": "Nome da categoria/conta gerencial (busca parcial). Ex: 'Supermercado', 'Transporte'" },
            "formaPagamentoNome": { "type": "string", "description": "Nome da forma de pagamento (busca parcial). Padrão: 'Crédito'." },
            "ehRecorrente": { "type": "boolean", "description": "Se true, cria recorrência mensal. Padrão: false." },
            "observacao": { "type": "string", "description": "Observação opcional para o lançamento." }
          },
          "required": ["faturaId", "conciliacaoId", "itemId", "descricao", "recebedorNome", "contaGerencialNome"]
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        var input = JsonNode.Parse(inputJson);

        if (!Guid.TryParse(input?["faturaId"]?.GetValue<string>(), out var faturaId))
            return JsonSerializer.Serialize(new { erro = "faturaId inválido." });
        if (!Guid.TryParse(input?["conciliacaoId"]?.GetValue<string>(), out var conciliacaoId))
            return JsonSerializer.Serialize(new { erro = "conciliacaoId inválido." });
        if (!Guid.TryParse(input?["itemId"]?.GetValue<string>(), out var itemId))
            return JsonSerializer.Serialize(new { erro = "itemId inválido." });

        var descricao = input?["descricao"]?.GetValue<string>();
        var recebedorNome = input?["recebedorNome"]?.GetValue<string>();
        var responsavelNome = input?["responsavelNome"]?.GetValue<string>();
        var contaGerencialNome = input?["contaGerencialNome"]?.GetValue<string>();
        var formaPagamentoNome = input?["formaPagamentoNome"]?.GetValue<string>() ?? "Crédito";
        var ehRecorrente = input?["ehRecorrente"]?.GetValue<bool>() ?? false;
        var observacao = input?["observacao"]?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(descricao))
            return JsonSerializer.Serialize(new { erro = "descricao é obrigatória." });
        if (string.IsNullOrWhiteSpace(recebedorNome))
            return JsonSerializer.Serialize(new { erro = "recebedorNome é obrigatório." });
        if (string.IsNullOrWhiteSpace(contaGerencialNome))
            return JsonSerializer.Serialize(new { erro = "contaGerencialNome é obrigatória." });

        var recebedor = await db.Pessoas.AsNoTracking()
            .Where(p => p.Nome.Contains(recebedorNome))
            .Select(p => new { p.Id, p.Nome })
            .FirstOrDefaultAsync(cancellationToken);

        if (recebedor is null)
        {
            var novaPessoa = Pessoa.Criar(
                nome: recebedorNome.Trim(),
                tipoPessoa: TipoPessoa.Juridica,
                cpfCnpj: null, email: null, telefone: null,
                observacao: "Criado automaticamente pela conciliação via agente IA",
                chavesPix: [], ativo: true);
            novaPessoa.AtribuirFamilia(context.FamiliaId);
            db.Pessoas.Add(novaPessoa);
            await db.SaveChangesAsync(cancellationToken);
            recebedor = new { novaPessoa.Id, Nome = recebedorNome };
        }

        Guid? responsavelId = null;
        if (!string.IsNullOrWhiteSpace(responsavelNome))
        {
            var responsavel = await db.Pessoas.AsNoTracking()
                .Where(p => p.Nome.Contains(responsavelNome))
                .Select(p => new { p.Id })
                .FirstOrDefaultAsync(cancellationToken);
            responsavelId = responsavel?.Id;
        }

        var contaGerencial = await db.ContasGerenciais.AsNoTracking()
            .Where(cg => cg.Descricao.Contains(contaGerencialNome) && cg.Ativo)
            .Select(cg => new { cg.Id, cg.Descricao })
            .FirstOrDefaultAsync(cancellationToken);

        if (contaGerencial is null)
            return JsonSerializer.Serialize(new
            {
                erro = $"Conta gerencial '{contaGerencialNome}' não encontrada.",
                contasDisponiveis = await db.ContasGerenciais.AsNoTracking().Where(cg => cg.Ativo)
                    .OrderBy(cg => cg.Descricao).Select(cg => cg.Descricao).Take(20).ToListAsync(cancellationToken)
            });

        var formaPagamento = await db.FormasPagamento.AsNoTracking()
            .Where(fp => fp.Nome.Contains(formaPagamentoNome) && fp.Ativo)
            .Select(fp => new { fp.Id })
            .FirstOrDefaultAsync(cancellationToken);

        if (formaPagamento is null)
            return JsonSerializer.Serialize(new
            {
                erro = $"Forma de pagamento '{formaPagamentoNome}' não encontrada.",
                formasDisponiveis = await db.FormasPagamento.AsNoTracking().Where(fp => fp.Ativo)
                    .OrderBy(fp => fp.Nome).Select(fp => fp.Nome).ToListAsync(cancellationToken)
            });

        var item = await db.ItensConciliacao.AsNoTracking()
            .Where(i => i.Id == itemId)
            .Select(i => new { i.Valor })
            .FirstOrDefaultAsync(cancellationToken);

        if (item is null)
            return JsonSerializer.Serialize(new { erro = "Item de conciliação não encontrado." });

        var rateios = new List<RateioRequest> { new(contaGerencial.Id, item.Valor) };

        RecorrenciaConfigRequest? recorrencia = null;
        if (ehRecorrente)
        {
            var fatura = await db.FaturasCartao.AsNoTracking()
                .Where(f => f.Id == faturaId)
                .Select(f => new { f.DataVencimento })
                .FirstOrDefaultAsync(cancellationToken);

            if (fatura is not null)
            {
                recorrencia = new RecorrenciaConfigRequest(
                    TipoPeriodicidadeRecorrencia.Mensal,
                    TipoDiaRecorrencia.DiaFixo,
                    fatura.DataVencimento.Day,
                    fatura.DataVencimento,
                    null,
                    false,
                    null);
            }
        }

        var request = new CriarItemFaturaRequest(
            descricao, recebedor.Id, responsavelId,
            formaPagamento.Id, rateios, recorrencia,
            null, observacao);

        try
        {
            var ok = await criarItem.CriarAsync(faturaId, conciliacaoId, itemId, request, cancellationToken);
            if (!ok)
                return JsonSerializer.Serialize(new { erro = "Item ou sessão de conciliação não encontrados." });

            return JsonSerializer.Serialize(new
            {
                sucesso = true,
                mensagem = "Lançamento criado e vinculado com sucesso. Preferências do estabelecimento foram salvas para próximas importações.",
                itemId,
                descricao,
                recebedor = recebedor.Nome,
                categoria = contaGerencial.Descricao,
                recorrente = ehRecorrente
            });
        }
        catch (Exception ex) when (ex.Message.Contains("Conciliacao") || ex.Message.Contains("item") || ex.Message.Contains("fatura") || ex.Message.Contains("rateio") || ex.Message.Contains("conta") || ex.Message.Contains("parcela") || ex.Message.Contains("Reabra"))
        {
            return JsonSerializer.Serialize(new { erro = ex.Message });
        }
    }
}
