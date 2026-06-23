using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Anexos;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Cadastros.FormasPagamento;
using ControleFinanceiro.Domain.Cadastros.Pessoas;
using ControleFinanceiro.Domain.Anexos;
using ControleFinanceiro.Domain.Financeiro;
using ControleFinanceiro.Domain.Identidade;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class CriarLancamentoTool(IAppDbContext db, AnexoAppService? anexoService = null) : IFinanceTool
{
    public string Name => "criar_lancamento";
    public string Description => "Cria uma ou mais parcelas de conta a pagar após confirmação do usuário e vincula comprovantes recebidos na conversa.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "descricao":         { "type": "string",  "description": "Descrição do gasto." },
            "valor":             { "type": "number",  "description": "Valor total da compra em reais." },
            "quantidadeParcelas":{ "type": "integer", "minimum": 1, "maximum": 60, "description": "Quantidade total de parcelas." },
            "contaGerencialId":  { "type": "string",  "description": "ID da categoria financeira." },
            "dataEmissao":       { "type": "string",  "description": "Data da compra no formato YYYY-MM-DD." },
            "dataVencimento":    { "type": "string",  "description": "Primeiro vencimento no formato YYYY-MM-DD." },
            "responsavelId":     { "type": "string",  "description": "ID da pessoa responsável pelo gasto." },
            "recebedorId":       { "type": "string",  "description": "ID do fornecedor." },
            "recebedorNome":     { "type": "string",  "description": "Nome do fornecedor para busca ou criação." },
            "formaPagamentoId":  { "type": "string",  "description": "ID da forma de pagamento." },
            "cartaoId":          { "type": "string",  "description": "ID do cartão para compra no crédito." },
            "contaBancariaId":   { "type": "string",  "description": "ID da conta bancária para pagamento com baixa automática." },
            "observacao":        { "type": "string",  "description": "Observação livre." }
          },
          "required": ["descricao", "valor", "contaGerencialId"]
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        if (context.Papel == PapelFamilia.Visualizador)
            return Error("Visualizadores não podem criar lançamentos.");

        var input = JsonNode.Parse(inputJson);
        var descricao = input?["descricao"]?.GetValue<string>()?.Trim() ?? string.Empty;
        var valor = input?["valor"]?.GetValue<decimal>() ?? 0;
        var quantidadeParcelas = input?["quantidadeParcelas"]?.GetValue<int?>() ?? 1;
        var contaGerencialIdStr = input?["contaGerencialId"]?.GetValue<string>();
        var dataEmissaoStr = input?["dataEmissao"]?.GetValue<string>();
        var dataVencimentoStr = input?["dataVencimento"]?.GetValue<string>();
        var responsavelIdStr = input?["responsavelId"]?.GetValue<string>();
        var recebedorIdStr = input?["recebedorId"]?.GetValue<string>();
        var recebedorNome = input?["recebedorNome"]?.GetValue<string>();
        var formaPagamentoIdStr = input?["formaPagamentoId"]?.GetValue<string>();
        var cartaoIdStr = input?["cartaoId"]?.GetValue<string>();
        var contaBancariaIdStr = input?["contaBancariaId"]?.GetValue<string>();
        var observacao = input?["observacao"]?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(descricao) || valor <= 0)
            return Error("Descrição e valor são obrigatórios e o valor deve ser maior que zero.");
        if (quantidadeParcelas is < 1 or > 60)
            return Error("Quantidade de parcelas deve estar entre 1 e 60.");
        if (!Guid.TryParse(contaGerencialIdStr, out var contaGerencialId))
            return Error("contaGerencialId inválido. Use listar_categorias para obter o ID correto.");

        var dataEmissao = DateOnly.TryParse(dataEmissaoStr, out var de) ? de : DateOnly.FromDateTime(DateTime.UtcNow);
        var dataVencimento = DateOnly.TryParse(dataVencimentoStr, out var dv) ? dv : dataEmissao;
        Guid? responsavelId = Guid.TryParse(responsavelIdStr, out var rid) ? rid : null;
        Guid? cartaoId = Guid.TryParse(cartaoIdStr, out var cid) ? cid : null;

        var formaPagamento = await ResolverFormaPagamentoAsync(formaPagamentoIdStr, cancellationToken);
        if (formaPagamento is null) return Error("Nenhuma forma de pagamento ativa foi encontrada.");
        if (formaPagamento.EhCartao && !cartaoId.HasValue) return Error("Informe o cartão usado na compra.");
        if (!formaPagamento.EhCartao && cartaoId.HasValue) return Error("Cartão só pode ser informado para uma forma de pagamento de cartão.");

        var recebedorId = await ResolverOuCriarRecebedorAsync(
            recebedorIdStr,
            recebedorNome ?? descricao,
            context.FamiliaId,
            cancellationToken);
        if (recebedorId == Guid.Empty) return Error("Não foi possível identificar ou criar o recebedor.");

        var rateio = RateioPlano.Create(contaGerencialId, valor);
        IReadOnlyCollection<ContaPagar> contas;

        if (cartaoId.HasValue)
        {
            var cartao = await db.Cartoes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == cartaoId.Value && x.Ativo, cancellationToken);
            if (cartao is null) return Error("Cartão não encontrado ou inativo.");

            contas = ContaPagar.CriarParcelasCartao(
                numeroDocumento: null,
                dataEmissao: dataEmissao,
                responsavelCompraId: responsavelId,
                recebedorId: recebedorId,
                formaPagamentoId: formaPagamento.Id,
                cartaoId: cartao.Id,
                valorOriginal: valor,
                valorDesconto: 0,
                valorJuros: 0,
                valorMulta: 0,
                quantidadeParcelas: quantidadeParcelas,
                origemCompraPlanejadaId: null,
                descricao: descricao,
                observacao: observacao,
                statusContaId: StatusConta.PendenteId,
                ehRecorrente: false,
                regraRecorrenciaId: null,
                origem: OrigemLancamento.AgenteIA,
                rateios: [rateio],
                diaFechamentoFatura: cartao.DiaFechamentoFatura,
                diaVencimentoFatura: cartao.DiaVencimentoFatura);
        }
        else
        {
            contas = ContaPagar.CriarParcelas(
                numeroDocumento: null,
                dataEmissao: dataEmissao,
                responsavelCompraId: responsavelId,
                recebedorId: recebedorId,
                dataVencimento: dataVencimento,
                formaPagamentoId: formaPagamento.Id,
                cartaoId: null,
                contaBancariaId: null,
                valorOriginal: valor,
                valorDesconto: 0,
                valorJuros: 0,
                valorMulta: 0,
                quantidadeParcelas: quantidadeParcelas,
                origemCompraPlanejadaId: null,
                descricao: descricao,
                observacao: observacao,
                statusContaId: StatusConta.PendenteId,
                ehRecorrente: false,
                regraRecorrenciaId: null,
                origem: OrigemLancamento.AgenteIA,
                rateios: [rateio]);
        }

        db.ContasPagar.AddRange(contas);
        db.RateiosContaGerencial.AddRange(contas.SelectMany(x => x.Rateios));

        if (formaPagamento.BaixarAutomaticamente)
        {
            var contaBancariaId = await ResolverContaBancariaAsync(contaBancariaIdStr, cancellationToken);
            if (!contaBancariaId.HasValue) return Error("Informe a conta bancária usada no pagamento.");

            foreach (var conta in contas)
            {
                var dataLiquidacao = dataEmissao.AddMonths(conta.NumeroParcela - 1);
                conta.Liquidar(dataLiquidacao, contaBancariaId.Value, StatusConta.LiquidadaId);
                db.MovimentacoesFinanceiras.Add(MovimentacaoFinanceira.CriarLiquidacaoContaPagar(
                    conta.Id,
                    contaBancariaId.Value,
                    dataLiquidacao,
                    conta.ValorLiquido,
                    StatusMovimentacao.EfetivadaId,
                    conta.Descricao));
            }
        }

        if (context.ConversaId.HasValue && anexoService is not null)
        {
            await anexoService.VincularPendentesDaConversaAsync(
                context.ConversaId.Value,
                TipoEntidadeAnexo.ContaPagar,
                contas.Select(x => x.Id).ToArray(),
                cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);

        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            lancamentoId = contas.First().Id,
            lancamentoIds = contas.Select(x => x.Id),
            quantidadeParcelas = contas.Count,
            mensagem = $"Lançamento criado: {descricao} - R$ {valor:N2} em {contas.Count} parcela(s)."
        });
    }

    private async Task<FormaPagamento?> ResolverFormaPagamentoAsync(string? formaPagamentoIdStr, CancellationToken ct)
    {
        if (Guid.TryParse(formaPagamentoIdStr, out var id))
            return await db.FormasPagamento.SingleOrDefaultAsync(x => x.Id == id && x.Ativo, ct);

        return await db.FormasPagamento
            .Where(x => x.Ativo)
            .OrderBy(x => x.EhCartao)
            .ThenBy(x => x.BaixarAutomaticamente)
            .ThenBy(x => x.Tipo)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<Guid?> ResolverContaBancariaAsync(string? contaBancariaIdStr, CancellationToken ct)
    {
        if (Guid.TryParse(contaBancariaIdStr, out var id))
            return await db.ContasBancarias.AnyAsync(x => x.Id == id && x.Ativo, ct) ? id : null;

        var ids = await db.ContasBancarias.Where(x => x.Ativo).Select(x => x.Id).Take(2).ToArrayAsync(ct);
        return ids.Length == 1 ? ids[0] : null;
    }

    private async Task<Guid> ResolverOuCriarRecebedorAsync(
        string? recebedorIdStr,
        string nome,
        Guid familiaId,
        CancellationToken ct)
    {
        if (Guid.TryParse(recebedorIdStr, out var recebedorId)) return recebedorId;

        var nomeTrimmed = nome.Trim();
        var existente = await db.Pessoas.AsNoTracking()
            .Where(p => EF.Functions.Like(p.Nome, nomeTrimmed))
            .Select(p => p.Id)
            .FirstOrDefaultAsync(ct);
        if (existente != Guid.Empty) return existente;

        var nova = Pessoa.Criar(
            nomeTrimmed,
            TipoPessoa.Juridica,
            null,
            null,
            null,
            "Criado automaticamente pelo agente IA",
            [],
            true);
        nova.AtribuirFamilia(familiaId);
        db.Pessoas.Add(nova);
        return nova.Id;
    }

    private static string Error(string message) => JsonSerializer.Serialize(new { erro = message });
}
