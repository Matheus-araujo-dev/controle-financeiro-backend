using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Cadastros.Pessoas;
using ControleFinanceiro.Domain.Financeiro;
using ControleFinanceiro.Domain.Identidade;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class CriarLancamentoTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "criar_lancamento";
    public string Description => "Cria um lançamento (conta a pagar) confirmado pelo usuário. Chame apenas após o usuário confirmar os dados do rascunho. Visualizador não pode criar lançamentos.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "descricao":         { "type": "string",  "description": "Descrição do gasto (ex: iFood, Mercado, etc.)" },
            "valor":             { "type": "number",  "description": "Valor em reais (ex: 45.90)" },
            "contaGerencialId":  { "type": "string",  "description": "ID da categoria financeira." },
            "dataVencimento":    { "type": "string",  "description": "Data no formato YYYY-MM-DD. Se omitida, usa hoje." },
            "responsavelId":     { "type": "string",  "description": "ID da Pessoa responsável pelo gasto. Se omitido, usa o solicitante." },
            "recebedorId":       { "type": "string",  "description": "ID da Pessoa recebedora (fornecedor). Prefira informar. Se omitido e recebedorNome informado, tenta encontrar ou criar." },
            "recebedorNome":     { "type": "string",  "description": "Nome do fornecedor/estabelecimento para busca ou criação automática quando recebedorId não conhecido." },
            "formaPagamentoId":  { "type": "string",  "description": "ID da forma de pagamento. Se omitido, usa a primeira disponível." },
            "cartaoId":          { "type": "string",  "description": "ID do cartão (apenas para compras no crédito)." },
            "observacao":        { "type": "string",  "description": "Observação livre (opcional)." }
          },
          "required": ["descricao", "valor", "contaGerencialId"]
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        if (context.Papel == PapelFamilia.Visualizador)
            return JsonSerializer.Serialize(new { erro = "Visualizadores não podem criar lançamentos." });

        var input = JsonNode.Parse(inputJson);
        var descricao = input?["descricao"]?.GetValue<string>() ?? string.Empty;
        var valor = input?["valor"]?.GetValue<decimal>() ?? 0;
        var contaGerencialIdStr = input?["contaGerencialId"]?.GetValue<string>();
        var dataVencimentoStr = input?["dataVencimento"]?.GetValue<string>();
        var responsavelIdStr = input?["responsavelId"]?.GetValue<string>();
        var recebedorIdStr = input?["recebedorId"]?.GetValue<string>();
        var recebedorNome = input?["recebedorNome"]?.GetValue<string>();
        var formaPagamentoIdStr = input?["formaPagamentoId"]?.GetValue<string>();
        var cartaoIdStr = input?["cartaoId"]?.GetValue<string>();
        var observacao = input?["observacao"]?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(descricao) || valor <= 0)
            return JsonSerializer.Serialize(new { erro = "Descrição e valor são obrigatórios e valor deve ser maior que zero." });

        if (!Guid.TryParse(contaGerencialIdStr, out var contaGerencialId))
            return JsonSerializer.Serialize(new { erro = "contaGerencialId inválido. Use listar_categorias para obter o ID correto." });

        var dataVencimento = DateOnly.TryParse(dataVencimentoStr, out var dv) ? dv : DateOnly.FromDateTime(DateTime.UtcNow);
        Guid? responsavelId = Guid.TryParse(responsavelIdStr, out var rid) ? rid : null;
        Guid? cartaoId = Guid.TryParse(cartaoIdStr, out var cid) ? cid : null;

        var formaPagamentoId = await ResolverFormaPagamentoAsync(formaPagamentoIdStr, cancellationToken);
        if (formaPagamentoId == Guid.Empty)
            return JsonSerializer.Serialize(new { erro = "Nenhuma forma de pagamento encontrada. Cadastre uma forma de pagamento no sistema." });

        var recebedorId = await ResolverOuCriarRecebedorAsync(
            recebedorIdStr, recebedorNome ?? descricao, context.FamiliaId, cancellationToken);

        if (recebedorId == Guid.Empty)
            return JsonSerializer.Serialize(new { erro = "Não foi possível identificar ou criar o recebedor. Informe recebedorId ou recebedorNome." });

        var rateio = RateioPlano.Create(contaGerencialId, valor);
        var conta = ContaPagar.Criar(
            numeroDocumento: null,
            dataEmissao: DateOnly.FromDateTime(DateTime.UtcNow),
            responsavelCompraId: responsavelId,
            recebedorId: recebedorId,
            dataVencimento: dataVencimento,
            formaPagamentoId: formaPagamentoId,
            cartaoId: cartaoId,
            contaBancariaId: null,
            valorOriginal: valor,
            valorDesconto: 0,
            valorJuros: 0,
            valorMulta: 0,
            quantidadeParcelas: 1,
            numeroParcela: 1,
            grupoParcelamentoId: null,
            origemCompraPlanejadaId: null,
            descricao: descricao,
            observacao: observacao,
            statusContaId: StatusConta.PendenteId,
            ehRecorrente: false,
            regraRecorrenciaId: null,
            origem: OrigemLancamento.AgenteIA,
            rateios: [rateio]);

        db.ContasPagar.Add(conta);
        await db.SaveChangesAsync(cancellationToken);

        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            lancamentoId = conta.Id,
            mensagem = $"Lançamento criado: {descricao} - R$ {valor:N2} vence em {dataVencimento:dd/MM/yyyy}."
        });
    }

    private async Task<Guid> ResolverFormaPagamentoAsync(string? formaPagamentoIdStr, CancellationToken ct)
    {
        if (Guid.TryParse(formaPagamentoIdStr, out var fid))
            return fid;

        var primeira = await db.FormasPagamento
            .AsNoTracking()
            .Where(f => f.Ativo)
            .OrderBy(f => f.Tipo)
            .Select(f => f.Id)
            .FirstOrDefaultAsync(ct);

        return primeira;
    }

    private async Task<Guid> ResolverOuCriarRecebedorAsync(
        string? recebedorIdStr, string nome, Guid familiaId, CancellationToken ct)
    {
        if (Guid.TryParse(recebedorIdStr, out var recebedorId))
            return recebedorId;

        var nomeTrimmed = nome.Trim();
        var nomeLower = nomeTrimmed.ToLower();
        var existente = await db.Pessoas
            .AsNoTracking()
            .Where(p => EF.Functions.Like(p.Nome.ToLower(), nomeLower))
            .Select(p => p.Id)
            .FirstOrDefaultAsync(ct);

        if (existente != Guid.Empty)
            return existente;

        var nova = Pessoa.Criar(
            nome: nomeTrimmed,
            tipoPessoa: TipoPessoa.Juridica,
            cpfCnpj: null,
            email: null,
            telefone: null,
            observacao: "Criado automaticamente pelo agente IA",
            chavesPix: [],
            ativo: true);
        nova.AtribuirFamilia(familiaId);
        db.Pessoas.Add(nova);

        return nova.Id;
    }
}
