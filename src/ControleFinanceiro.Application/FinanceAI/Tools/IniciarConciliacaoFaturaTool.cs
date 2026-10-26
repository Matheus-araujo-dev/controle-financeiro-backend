using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Conciliacao;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class IniciarConciliacaoFaturaTool(IAppDbContext db, ConciliacaoFaturaAppService conciliacao) : IFinanceTool
{
    public string Name => "iniciar_conciliacao_fatura";
    public string Description => "Inicia conciliação de fatura de cartão a partir de um PDF anexado. Faz parse do PDF, identifica itens e encontra correspondências inteligentes com lançamentos já existentes (fuzzy matching por valor, data e nome). Retorna preview com itens pendentes, candidatos e sugestões aprendidas.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "cartaoNome": { "type": "string", "description": "Nome do cartão (ex: 'Mercado Pago', 'Nubank'). Busca parcial." },
            "faturaId": { "type": "string", "description": "ID da fatura (GUID). Se informado, usa diretamente; senão, busca a fatura aberta do cartão." }
          },
          "required": []
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        var input = JsonNode.Parse(inputJson);
        var cartaoNome = input?["cartaoNome"]?.GetValue<string>();
        var faturaIdStr = input?["faturaId"]?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(context.ArquivoAnexoPath))
            return JsonSerializer.Serialize(new { erro = "Nenhum arquivo PDF foi anexado. Peça ao usuário para enviar o PDF da fatura." });

        if (!File.Exists(context.ArquivoAnexoPath))
            return JsonSerializer.Serialize(new { erro = "Arquivo anexado não encontrado no servidor." });

        Guid? faturaId = null;
        if (!string.IsNullOrWhiteSpace(faturaIdStr) && Guid.TryParse(faturaIdStr, out var fid))
            faturaId = fid;

        if (faturaId is null)
        {
            if (string.IsNullOrWhiteSpace(cartaoNome))
                return JsonSerializer.Serialize(new { erro = "Informe cartaoNome ou faturaId para identificar a fatura." });

            var cartoes = await db.Cartoes.AsNoTracking()
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

            var fatura = await db.FaturasCartao.AsNoTracking()
                .Where(f => f.CartaoId == cartao.Id && f.Status == Domain.Financeiro.StatusFaturaCartao.Aberta)
                .OrderBy(f => f.DataVencimento)
                .Select(f => new { f.Id, f.DataVencimento })
                .FirstOrDefaultAsync(cancellationToken);

            if (fatura is null)
                return JsonSerializer.Serialize(new { erro = $"Nenhuma fatura aberta encontrada para o cartão '{cartao.Nome}'." });

            faturaId = fatura.Id;
        }

        var fileName = context.ArquivoAnexoNome ?? "fatura.pdf";
        try
        {
            await using var stream = new FileStream(context.ArquivoAnexoPath, FileMode.Open, FileAccess.Read);
            var resultado = await conciliacao.IniciarAsync(faturaId.Value, fileName, stream, cancellationToken);

            if (resultado is null)
                return JsonSerializer.Serialize(new { erro = "Fatura não encontrada." });

            var pendentes = resultado.Itens.Where(i => i.Status == "Pendente").ToArray();
            var conciliados = resultado.Itens.Where(i => i.Status == "Conciliado").ToArray();
            var ignorados = resultado.Itens.Where(i => i.Status == "Ignorado").ToArray();
            var comCorrespondenciaClara = pendentes.Count(i => i.Candidatos.Any(c => c.CorrespondenciaClara));

            return JsonSerializer.Serialize(new
            {
                conciliacaoId = resultado.Id,
                faturaId = resultado.FaturaId,
                arquivo = resultado.NomeArquivo,
                avisoLeitura = resultado.AvisoLeitura,
                totalDocumento = resultado.TotalDocumento,
                resumo = new
                {
                    totalItens = resultado.Itens.Count,
                    pendentes = pendentes.Length,
                    conciliados = conciliados.Length,
                    ignorados = ignorados.Length,
                    comCorrespondenciaClara,
                    valorTotalPendente = pendentes.Sum(i => i.Valor),
                    valorTotalConciliado = conciliados.Sum(i => i.Valor)
                },
                instrucao = pendentes.Length > 0
                    ? $"Há {pendentes.Length} itens pendentes ({comCorrespondenciaClara} com correspondência clara). Use listar_itens_pendentes_conciliacao para ver os detalhes e conciliar item a item, ou direcione o usuário para a tela de conciliação no app."
                    : "Todos os itens já foram processados."
            });
        }
        catch (Exception ex) when (ex.Message.Contains("PDF") || ex.Message.Contains("fatura") || ex.Message.Contains("lançamento") || ex.Message.Contains("vencimento") || ex.Message.Contains("senha") || ex.Message.Contains("Arquivo"))
        {
            return JsonSerializer.Serialize(new { erro = ex.Message });
        }
    }
}
