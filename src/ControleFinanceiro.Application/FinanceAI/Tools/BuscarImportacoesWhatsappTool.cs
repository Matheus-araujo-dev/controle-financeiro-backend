using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.ImportacoesWhatsapp;
using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Contracts.ImportacoesWhatsapp;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class BuscarImportacoesWhatsappTool(ImportacoesWhatsappAppService service) : IFinanceTool
{
    public string Name => "buscar_importacoes_whatsapp";
    public string Description => "Consulta importações do WhatsApp. Sem parâmetros retorna lista das recentes; com importacaoId retorna detalhe com itens extraídos e suas predições.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "importacaoId": { "type": "string", "description": "ID da importação (GUID). Se informado, retorna detalhe completo com itens." },
            "status":       { "type": "string", "description": "Filtrar por status: Recebido, EmProcessamento, ExtraidoComSucesso, ErroExtracao, PendenteRevisao, Confirmado" },
            "remetente":    { "type": "string", "description": "Filtrar por remetente (busca parcial)" },
            "pagina":       { "type": "integer", "description": "Página (padrão 1)" },
            "tamanhoPagina": { "type": "integer", "description": "Itens por página (padrão 20)" }
          },
          "required": []
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        var input = JsonNode.Parse(inputJson);
        var importacaoIdStr = input?["importacaoId"]?.GetValue<string>();

        if (!string.IsNullOrWhiteSpace(importacaoIdStr))
        {
            if (!Guid.TryParse(importacaoIdStr, out var importacaoId))
                return JsonSerializer.Serialize(new { erro = "importacaoId deve ser um GUID válido." });

            var detalhe = await service.ObterPorIdAsync(importacaoId, cancellationToken);
            if (detalhe is null)
                return JsonSerializer.Serialize(new { erro = "Importação não encontrada." });

            return JsonSerializer.Serialize(new
            {
                id = detalhe.Id,
                tipoOrigem = detalhe.TipoOrigemNome,
                remetente = detalhe.Remetente,
                textoBruto = detalhe.TextoBruto,
                nomeArquivo = detalhe.NomeArquivo,
                status = detalhe.StatusNome,
                statusCodigo = detalhe.StatusCodigo,
                confiancaExtracao = detalhe.ConfiancaExtracao,
                mensagemErro = detalhe.MensagemErro,
                possuiGeracaoFinanceira = detalhe.PossuiGeracaoFinanceira,
                recebidoEm = detalhe.RecebidoEmUtc.ToString("yyyy-MM-dd HH:mm"),
                processadoEm = detalhe.ProcessadoEmUtc?.ToString("yyyy-MM-dd HH:mm"),
                totalItens = detalhe.Itens.Count,
                itens = detalhe.Itens.Select(i => new
                {
                    id = i.Id,
                    tipoSugestao = i.TipoSugestaoNome,
                    payloadSugerido = i.PayloadSugeridoJson,
                    status = i.StatusNome,
                    statusCodigo = i.StatusCodigo,
                    descricaoAjustada = i.DescricaoAjustada,
                    contaGerencial = i.ContaGerencialDescricao,
                    contaGerencialId = i.ContaGerencialId,
                    pagador = i.PagadorNome,
                    pagadorId = i.PagadorId,
                    marcarComoRecorrente = i.MarcarComoRecorrente,
                    gerarContaReceber = i.ContaReceberId.HasValue,
                    predicao = i.Predicao is null ? null : new
                    {
                        contaGerencial = i.Predicao.ContaGerencialDescricao,
                        contaGerencialId = i.Predicao.ContaGerencialId,
                        pagador = i.Predicao.PagadorNome,
                        pagadorId = i.Predicao.PagadorId,
                        descricaoAjustada = i.Predicao.DescricaoAjustada,
                        gerarContaReceber = i.Predicao.GerarContaReceber,
                        marcarComoRecorrente = i.Predicao.MarcarComoRecorrente,
                        confianca = i.Predicao.ConfiancaHistorico,
                        ocorrencias = i.Predicao.QuantidadeOcorrencias
                    }
                })
            });
        }

        var query = new ImportacaoWhatsappListQueryRequest
        {
            StatusCodigo = input?["status"]?.GetValue<string>(),
            Remetente = input?["remetente"]?.GetValue<string>(),
            Page = input?["pagina"]?.GetValue<int>() ?? 1,
            PageSize = input?["tamanhoPagina"]?.GetValue<int>() ?? 20,
            SortBy = "RecebidoEmUtc",
            SortDirection = SortDirection.Desc
        };

        var resultado = await service.ListarAsync(query, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            pagina = resultado.Page,
            totalPaginas = resultado.TotalPages,
            totalItens = resultado.TotalItems,
            importacoes = resultado.Items.Select(i => new
            {
                id = i.Id,
                tipoOrigem = i.TipoOrigemNome,
                remetente = i.Remetente,
                nomeArquivo = i.NomeArquivo,
                status = i.StatusNome,
                statusCodigo = i.StatusCodigo,
                confiancaExtracao = i.ConfiancaExtracao,
                quantidadeItens = i.QuantidadeItens,
                quantidadePendentes = i.QuantidadePendentes,
                recebidoEm = i.RecebidoEmUtc.ToString("yyyy-MM-dd HH:mm")
            })
        });
    }
}
