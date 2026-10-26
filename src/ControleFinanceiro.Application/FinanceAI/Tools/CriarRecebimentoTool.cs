using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Cadastros.Pessoas;
using ControleFinanceiro.Domain.Financeiro;
using ControleFinanceiro.Domain.Identidade;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class CriarRecebimentoTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "criar_recebimento";
    public string Description => "Cria uma conta a receber confirmada pelo usuário. Chame apenas após o usuário confirmar os dados do rascunho. Visualizador não pode criar.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "descricao":         { "type": "string",  "description": "Descrição do recebimento (ex: Salário, Freelance, etc.)" },
            "valor":             { "type": "number",  "description": "Valor em reais (ex: 3500.00)" },
            "contaGerencialId":  { "type": "string",  "description": "ID da categoria financeira." },
            "dataVencimento":    { "type": "string",  "description": "Data no formato YYYY-MM-DD. Se omitida, usa hoje." },
            "pagadorId":         { "type": "string",  "description": "ID da Pessoa pagadora. Se omitido e pagadorNome informado, tenta encontrar ou criar." },
            "pagadorNome":       { "type": "string",  "description": "Nome do pagador para busca ou criação automática quando pagadorId não conhecido." },
            "formaPagamentoId":  { "type": "string",  "description": "ID da forma de pagamento. Se omitido, usa a primeira disponível." },
            "observacao":        { "type": "string",  "description": "Observação livre (opcional)." }
          },
          "required": ["descricao", "valor", "contaGerencialId"]
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        if (context.Papel == PapelFamilia.Visualizador)
            return JsonSerializer.Serialize(new { erro = "Visualizadores não podem criar recebimentos." });

        var input = JsonNode.Parse(inputJson);
        var descricao = input?["descricao"]?.GetValue<string>() ?? string.Empty;
        var valor = input?["valor"]?.GetValue<decimal>() ?? 0;
        var contaGerencialIdStr = input?["contaGerencialId"]?.GetValue<string>();
        var dataVencimentoStr = input?["dataVencimento"]?.GetValue<string>();
        var pagadorIdStr = input?["pagadorId"]?.GetValue<string>();
        var pagadorNome = input?["pagadorNome"]?.GetValue<string>();
        var formaPagamentoIdStr = input?["formaPagamentoId"]?.GetValue<string>();
        var observacao = input?["observacao"]?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(descricao) || valor <= 0)
            return JsonSerializer.Serialize(new { erro = "Descrição e valor são obrigatórios e valor deve ser maior que zero." });

        if (!Guid.TryParse(contaGerencialIdStr, out var contaGerencialId))
            return JsonSerializer.Serialize(new { erro = "contaGerencialId inválido. Use listar_categorias para obter o ID correto." });

        var dataVencimento = DateOnly.TryParse(dataVencimentoStr, out var dv) ? dv : DateOnly.FromDateTime(DateTime.UtcNow);

        var formaPagamentoId = await ResolverFormaPagamentoAsync(formaPagamentoIdStr, cancellationToken);
        if (formaPagamentoId == Guid.Empty)
            return JsonSerializer.Serialize(new { erro = "Nenhuma forma de pagamento encontrada." });

        var pagadorId = await ResolverOuCriarPagadorAsync(
            pagadorIdStr, pagadorNome ?? descricao, context.FamiliaId, cancellationToken);

        if (pagadorId == Guid.Empty)
            return JsonSerializer.Serialize(new { erro = "Não foi possível identificar ou criar o pagador. Informe pagadorId ou pagadorNome." });

        var rateio = RateioPlano.Create(contaGerencialId, valor);
        var conta = ContaReceber.Criar(
            numeroDocumento: null,
            dataEmissao: DateOnly.FromDateTime(DateTime.UtcNow),
            responsavelId: null,
            pagadorId: pagadorId,
            dataVencimento: dataVencimento,
            formaPagamentoId: formaPagamentoId,
            cartaoId: null,
            contaBancariaId: null,
            valorOriginal: valor,
            valorDesconto: 0,
            valorJuros: 0,
            valorMulta: 0,
            quantidadeParcelas: 1,
            numeroParcela: 1,
            grupoParcelamentoId: null,
            descricao: descricao,
            observacao: observacao,
            statusContaId: StatusConta.PendenteId,
            ehRecorrente: false,
            regraRecorrenciaId: null,
            origem: OrigemLancamento.AgenteIA,
            rateios: [rateio]);

        db.ContasReceber.Add(conta);
        await db.SaveChangesAsync(cancellationToken);

        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            recebimentoId = conta.Id,
            mensagem = $"Recebimento criado: {descricao} - R$ {valor:N2} vence em {dataVencimento:dd/MM/yyyy}."
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

    private async Task<Guid> ResolverOuCriarPagadorAsync(
        string? pagadorIdStr, string nome, Guid familiaId, CancellationToken ct)
    {
        if (Guid.TryParse(pagadorIdStr, out var pagadorId))
            return pagadorId;

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
            tipoPessoa: TipoPessoa.Fisica,
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
