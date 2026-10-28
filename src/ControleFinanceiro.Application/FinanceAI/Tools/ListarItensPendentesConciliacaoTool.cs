using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Conciliacao;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class ListarItensPendentesConciliacaoTool(IAppDbContext db, ConciliacaoFaturaAppService conciliacao) : IFinanceTool
{
    public string Name => "listar_itens_pendentes_conciliacao";
    public string Description => "Lista itens pendentes de uma sessão de conciliação, com candidatos de correspondência e preferências aprendidas. Retorna página por página para conciliar item a item via chat.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "conciliacaoId": { "type": "string", "description": "ID da sessão de conciliação (GUID)" },
            "faturaId": { "type": "string", "description": "ID da fatura (GUID)" },
            "pagina": { "type": "integer", "description": "Número da página (1-based, padrão 1). Cada página traz 5 itens." },
            "apenasProximo": { "type": "boolean", "description": "Se true, retorna apenas o próximo item pendente (para conciliar um a um)." }
          },
          "required": ["conciliacaoId", "faturaId"]
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        var input = JsonNode.Parse(inputJson);
        var conciliacaoIdStr = input?["conciliacaoId"]?.GetValue<string>();
        var faturaIdStr = input?["faturaId"]?.GetValue<string>();
        var pagina = input?["pagina"]?.GetValue<int>() ?? 1;
        var apenasProximo = input?["apenasProximo"]?.GetValue<bool>() ?? false;

        if (!Guid.TryParse(conciliacaoIdStr, out var conciliacaoId))
            return JsonSerializer.Serialize(new { erro = "conciliacaoId inválido." });
        if (!Guid.TryParse(faturaIdStr, out var faturaId))
            return JsonSerializer.Serialize(new { erro = "faturaId inválido." });

        var resultado = await conciliacao.ObterAsync(faturaId, conciliacaoId, cancellationToken);
        if (resultado is null)
            return JsonSerializer.Serialize(new { erro = "Sessão de conciliação não encontrada." });

        var pendentes = resultado.Itens.Where(i => i.Status == "Pendente").ToArray();

        if (pendentes.Length == 0)
        {
            var conciliados = resultado.Itens.Count(i => i.Status == "Conciliado");
            var ignorados = resultado.Itens.Count(i => i.Status == "Ignorado");
            return JsonSerializer.Serialize(new
            {
                mensagem = "Todos os itens já foram processados!",
                resumoFinal = new { totalItens = resultado.Itens.Count, conciliados, ignorados }
            });
        }

        var itensPagina = apenasProximo
            ? pendentes.Take(1).ToArray()
            : pendentes.Skip((pagina - 1) * 5).Take(5).ToArray();

        var pessoaIds = resultado.ContasSistema
            .SelectMany(c => new[] { c.PagadorId, c.RecebedorId })
            .Where(id => id.HasValue).Select(id => id!.Value)
            .Concat(pendentes.Where(p => p.Preferencias is not null)
                .SelectMany(p => new[] { p.Preferencias!.PagadorId, p.Preferencias.RecebedorId }
                    .Where(id => id.HasValue).Select(id => id!.Value)))
            .Distinct().ToArray();

        var pessoas = pessoaIds.Length > 0
            ? await db.Pessoas.AsNoTracking()
                .Where(p => pessoaIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Nome })
                .ToDictionaryAsync(p => p.Id, p => p.Nome, cancellationToken)
            : new Dictionary<Guid, string>();

        var contaGerIds = resultado.ContasSistema
            .SelectMany(c => c.Rateios.Select(r => r.ContaGerencialId))
            .Distinct().ToArray();

        var contasGer = contaGerIds.Length > 0
            ? await db.ContasGerenciais.AsNoTracking()
                .Where(cg => contaGerIds.Contains(cg.Id))
                .Select(cg => new { cg.Id, cg.Descricao })
                .ToDictionaryAsync(cg => cg.Id, cg => cg.Descricao, cancellationToken)
            : new Dictionary<Guid, string>();

        string NomePessoa(Guid? id) => id.HasValue && pessoas.TryGetValue(id.Value, out var n) ? n : "";
        string NomeConta(Guid id) => contasGer.TryGetValue(id, out var n) ? n : id.ToString();

        return JsonSerializer.Serialize(new
        {
            conciliacaoId,
            faturaId,
            resumo = new
            {
                totalPendentes = pendentes.Length,
                paginaAtual = apenasProximo ? 1 : pagina,
                totalPaginas = apenasProximo ? pendentes.Length : (int)Math.Ceiling(pendentes.Length / 5.0),
                valorTotalPendente = pendentes.Sum(i => i.Valor)
            },
            itens = itensPagina.Select(item => new
            {
                itemId = item.Id,
                data = item.Data.ToString("dd/MM/yyyy"),
                descricaoOriginal = item.DescricaoOriginal,
                valor = item.Valor,
                parcela = item.QuantidadeParcelas > 1 ? $"{item.NumeroParcela}/{item.QuantidadeParcelas}" : (string?)null,
                candidatos = item.Candidatos.Select(c => new
                {
                    contaPagarId = c.ContaId,
                    descricaoSistema = resultado.ContasSistema.FirstOrDefault(cs => cs.Id == c.ContaId)?.Descricao,
                    valorSistema = resultado.ContasSistema.FirstOrDefault(cs => cs.Id == c.ContaId)?.Valor,
                    pagador = NomePessoa(resultado.ContasSistema.FirstOrDefault(cs => cs.Id == c.ContaId)?.PagadorId),
                    diferenca = c.Diferenca,
                    pontos = c.Pontos,
                    motivo = c.Motivo,
                    correspondenciaClara = c.CorrespondenciaClara,
                    categorias = resultado.ContasSistema.FirstOrDefault(cs => cs.Id == c.ContaId)?.Rateios
                        .Select(r => new { contaGerencial = NomeConta(r.ContaGerencialId), r.Valor })
                }),
                preferenciaAprendida = item.Preferencias is null ? null : new
                {
                    descricaoSugerida = item.Preferencias.Descricao,
                    pagador = NomePessoa(item.Preferencias.PagadorId),
                    recebedor = NomePessoa(item.Preferencias.RecebedorId),
                    ehRecorrente = item.Preferencias.EhRecorrente,
                    gerarReembolso = item.Preferencias.GerarReembolso
                }
            })
        });
    }
}
