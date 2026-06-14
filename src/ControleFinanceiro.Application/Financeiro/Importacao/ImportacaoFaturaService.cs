using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Application.FinanceAI;
using ControleFinanceiro.Contracts.Financeiro.ImportacaoFatura;
using ControleFinanceiro.Domain.Cadastros.Cartoes;
using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ControleFinanceiro.Application.Financeiro.Importacao;

public sealed class ImportacaoFaturaService(
    IAppDbContext db,
    ILogger<ImportacaoFaturaService> logger,
    ILlmClient? llmClient = null)
{
    public async Task<ImportacaoFaturaPreviewResponse> GerarPreviewAsync(
        Guid cartaoId,
        Stream arquivoStream,
        string nomeArquivo,
        Guid familiaId,
        CancellationToken cancellationToken)
    {
        var ext = Path.GetExtension(nomeArquivo).ToLowerInvariant();

        CsvFaturaParser.ParseResult parseResult;
        if (ext == ".pdf")
        {
            // Mantém stream legível para o fallback IA
            var pdfBytes = new MemoryStream();
            await arquivoStream.CopyToAsync(pdfBytes, cancellationToken);
            pdfBytes.Position = 0;

            parseResult = PdfFaturaParser.Parse(pdfBytes);

            // Fallback: se regex não extraiu nada, usa IA para interpretar o texto bruto
            if (parseResult.Itens.Count == 0 && llmClient is not null)
            {
                pdfBytes.Position = 0;
                parseResult = await ExtrairComIaAsync(pdfBytes, cancellationToken);
            }
        }
        else if (ext == ".ofx")
        {
            using var reader = new StreamReader(arquivoStream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var conteudo = await reader.ReadToEndAsync(cancellationToken);
            parseResult = OfxFaturaParser.Parse(conteudo);
        }
        else
        {
            using var reader = new StreamReader(arquivoStream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var conteudo = await reader.ReadToEndAsync(cancellationToken);
            parseResult = CsvFaturaParser.Parse(conteudo);
        }

        if (parseResult.Itens.Count == 0)
        {
            return new ImportacaoFaturaPreviewResponse(
                [],
                0,
                0,
                parseResult.AvisoFormato ?? "Nenhum item encontrado no arquivo.");
        }

        // Verifica quais chaves já foram importadas para este cartão
        var chaves = parseResult.Itens
            .Select(i => $"{cartaoId}|{CsvFaturaParser.GerarChave(i)}")
            .ToHashSet();

        var jaImportadas = await db.ContasPagar
            .AsNoTracking()
            .Where(cp => cp.CartaoId == cartaoId
                      && cp.ChaveSerieImportacaoCartao != null
                      && chaves.Contains(cp.ChaveSerieImportacaoCartao))
            .Select(cp => cp.ChaveSerieImportacaoCartao!)
            .ToListAsync(cancellationToken);

        var jaImportadasSet = jaImportadas.ToHashSet();

        var itens = parseResult.Itens.Select(i =>
        {
            var chave = CsvFaturaParser.GerarChave(i);
            var chaveCompleta = $"{cartaoId}|{chave}";
            return new ImportacaoFaturaItemPreview(
                i.DataTransacao,
                i.Descricao,
                i.Valor,
                jaImportadasSet.Contains(chaveCompleta),
                chave);
        }).ToList();

        var novos = itens.Where(i => !i.JaImportado).ToList();

        return new ImportacaoFaturaPreviewResponse(
            itens,
            novos.Sum(i => i.Valor),
            novos.Count,
            parseResult.AvisoFormato);
    }

    public async Task<ConfirmarImportacaoFaturaResponse> ConfirmarAsync(
        ConfirmarImportacaoFaturaRequest request,
        Guid familiaId,
        CancellationToken cancellationToken)
    {
        var cartao = await db.Cartoes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CartaoId && c.FamiliaId == familiaId, cancellationToken);

        if (cartao is null)
            throw ValidationExceptionFactory.Create("CartaoId", "Cartao nao encontrado.");

        if (cartao is null)
            throw new InvalidOperationException("Cartão não encontrado.");

        // Verifica quais chaves já estão no banco (dedup)
        var formaPagamentoValida = await db.FormasPagamento
            .AsNoTracking()
            .AnyAsync(
                f => f.Id == request.FormaPagamentoId && f.FamiliaId == familiaId && f.Ativo && f.EhCartao,
                cancellationToken);

        if (!formaPagamentoValida)
            throw ValidationExceptionFactory.Create("FormaPagamentoId", "Forma de pagamento de cartao nao encontrada.");

        var recebedorValido = await db.Pessoas
            .AsNoTracking()
            .AnyAsync(
                p => p.Id == request.RecebedorPadraoId && p.FamiliaId == familiaId && p.Ativo,
                cancellationToken);

        if (!recebedorValido)
            throw ValidationExceptionFactory.Create("RecebedorPadraoId", "Recebedor padrao nao encontrado.");

        var contaGerencialValida = await db.ContasGerenciais
            .AsNoTracking()
            .AnyAsync(
                c => c.Id == request.ContaGerencialPadraoId
                     && c.FamiliaId == familiaId
                     && c.Ativo
                     && c.Tipo == TipoContaGerencial.Despesa
                     && !db.ContasGerenciais.Any(child => child.ContaPaiId == c.Id),
                cancellationToken);

        if (!contaGerencialValida)
            throw ValidationExceptionFactory.Create("ContaGerencialPadraoId", "Conta gerencial de despesa nao encontrada ou nao aceita lancamentos.");

        var chavesCompletas = request.Itens
            .Select(i => $"{request.CartaoId}|{i.ChaveImportacao}")
            .ToList();

        var existentes = await db.ContasPagar
            .AsNoTracking()
            .Where(cp => cp.CartaoId == request.CartaoId
                      && cp.ChaveSerieImportacaoCartao != null
                      && chavesCompletas.Contains(cp.ChaveSerieImportacaoCartao))
            .Select(cp => cp.ChaveSerieImportacaoCartao!)
            .ToListAsync(cancellationToken);

        var existentesSet = existentes.ToHashSet();

        var criadas = 0;
        var duplicadas = 0;

        foreach (var item in request.Itens)
        {
            var chaveCompleta = $"{request.CartaoId}|{item.ChaveImportacao}";

            if (existentesSet.Contains(chaveCompleta))
            {
                duplicadas++;
                continue;
            }

            var categoriaId = item.ContaGerencialId ?? request.ContaGerencialPadraoId;

            var parcelas = ContaPagar.CriarParcelasCartao(
                numeroDocumento: null,
                dataEmissao: item.DataTransacao,
                responsavelCompraId: null,
                recebedorId: request.RecebedorPadraoId,
                formaPagamentoId: request.FormaPagamentoId,
                cartaoId: request.CartaoId,
                valorOriginal: item.Valor,
                valorDesconto: 0m,
                valorJuros: 0m,
                valorMulta: 0m,
                quantidadeParcelas: 1,
                origemCompraPlanejadaId: null,
                descricao: item.Descricao,
                observacao: "Importação de fatura",
                statusContaId: StatusConta.EmFaturaId,
                ehRecorrente: false,
                regraRecorrenciaId: null,
                origem: OrigemLancamento.Importacao,
                rateios:
                [
                    RateioPlano.CreateSigned(categoriaId, item.Valor)
                ],
                diaFechamentoFatura: cartao.DiaFechamentoFatura,
                diaVencimentoFatura: cartao.DiaVencimentoFatura);

            foreach (var parcela in parcelas)
            {
                parcela.DefinirChaveSerieImportacaoCartao(chaveCompleta);
                parcela.AtribuirFamilia(familiaId);
                db.ContasPagar.Add(parcela);
                db.RateiosContaGerencial.AddRange(parcela.Rateios);
            }

            criadas++;
        }

        if (criadas > 0)
            await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Importação de fatura: {Criadas} criadas, {Duplicadas} duplicadas (cartão {CartaoId})",
            criadas, duplicadas, request.CartaoId);

        return new ConfirmarImportacaoFaturaResponse(criadas, duplicadas);
    }

    // ─── Fallback: extração via IA quando regex falha ────────────────────────

    private async Task<CsvFaturaParser.ParseResult> ExtrairComIaAsync(
        Stream pdfStream, CancellationToken cancellationToken)
    {
        var textoExtraido = PdfFaturaParser.ExtrairTexto(pdfStream);
        if (string.IsNullOrWhiteSpace(textoExtraido))
            return new CsvFaturaParser.ParseResult([], "O PDF não contém texto legível (pode ser escaneado).");

        // Limita o texto para não estourar tokens (4000 chars ~= 1000 tokens)
        if (textoExtraido.Length > 4000) textoExtraido = textoExtraido[..4000];

        var systemPrompt = """
            Você é um extrator de transações financeiras de extratos bancários brasileiros.
            Dado o texto de um extrato, extraia todas as transações de despesa (saídas, compras, débitos).
            Retorne SOMENTE um JSON com este formato, sem markdown, sem explicações:
            {"transacoes":[{"data":"dd/MM/yyyy","descricao":"texto","valor":0.00},...]}
            Regras:
            - Ignore receitas, depósitos, créditos e transferências recebidas.
            - valor deve ser positivo (valor em reais, sem R$).
            - data no formato dd/MM/yyyy.
            - descricao limitada a 100 caracteres.
            - Se não houver transações, retorne {"transacoes":[]}.
            """;

        var messages = new List<LlmMessage>
        {
            new(LlmRole.User, $"Extrato bancário para processar:\n\n{textoExtraido}")
        };

        try
        {
            var request = new LlmRequest(LlmModelTier.Reasoning, systemPrompt, messages);
            var completion = await llmClient!.CompleteAsync(request, cancellationToken);
            var json = completion.Text?.Trim() ?? "{}";

            // Remove markdown code blocks se presentes
            if (json.StartsWith("```"))
            {
                var lines = json.Split('\n');
                json = string.Join('\n', lines[1..^1]);
            }

            var node = JsonNode.Parse(json);
            var array = node?["transacoes"]?.AsArray() ?? [];
            var itens = new List<CsvFaturaItem>();

            foreach (var item in array)
            {
                if (item is null) continue;
                var dataStr = item["data"]?.GetValue<string>();
                var descricao = item["descricao"]?.GetValue<string>();
                var valorStr = item["valor"]?.ToString();

                if (!DateOnly.TryParseExact(dataStr, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data)) continue;
                if (!decimal.TryParse(valorStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var valor) || valor <= 0) continue;
                if (string.IsNullOrWhiteSpace(descricao)) continue;

                itens.Add(new CsvFaturaItem(data, descricao.Length > 100 ? descricao[..100] : descricao, valor));
            }

            logger.LogInformation("Fallback IA para PDF: {Count} transações extraídas", itens.Count);

            return itens.Count > 0
                ? new CsvFaturaParser.ParseResult(itens, "Transações extraídas via Inteligência Artificial — revise antes de confirmar.")
                : new CsvFaturaParser.ParseResult([], "A IA não conseguiu identificar transações neste PDF. Tente exportar como CSV.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Fallback IA falhou ao extrair PDF");
            return new CsvFaturaParser.ParseResult([], "Não foi possível processar o PDF. Tente exportar como CSV.");
        }
    }
}
