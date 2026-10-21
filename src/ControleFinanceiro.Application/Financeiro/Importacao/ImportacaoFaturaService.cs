using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Contracts.Financeiro.ImportacaoFatura;
using ControleFinanceiro.Domain.Cadastros.Cartoes;
using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ControleFinanceiro.Application.Financeiro.Importacao;

public sealed class ImportacaoFaturaService(
    IAppDbContext db,
    ILogger<ImportacaoFaturaService> logger,
    IPdfFaturaReader pdfReader)
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
            parseResult = await pdfReader.ParseAsync(arquivoStream, cancellationToken);
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

        // Compras iguais no mesmo PDF são ocorrências independentes, com chaves estáveis na reimportação.
        var occurrences = new Dictionary<string, int>();
        var keyedItems = parseResult.Itens.Select(item =>
        {
            var key = CsvFaturaParser.GerarChave(item);
            if (item.DataVencimentoFatura.HasValue)
            {
                occurrences.TryGetValue(key, out var count);
                occurrences[key] = ++count;
                if (count > 1) key += $"-{count}";
            }
            return (Item: item, Key: key);
        }).ToArray();
        var chaves = keyedItems
            .Select(i => $"{cartaoId}|{i.Key}")
            .ToHashSet();

        var jaImportadas = await db.ContasPagar
            .AsNoTracking()
            .Where(cp => cp.CartaoId == cartaoId
                      && cp.ChaveSerieImportacaoCartao != null
                      && chaves.Contains(cp.ChaveSerieImportacaoCartao))
            .Select(cp => cp.ChaveSerieImportacaoCartao!)
            .ToListAsync(cancellationToken);

        var jaImportadasSet = jaImportadas.ToHashSet();

        var itens = keyedItems.Select(entry =>
        {
            var i = entry.Item;
            var chave = entry.Key;
            var chaveCompleta = $"{cartaoId}|{chave}";
            return new ImportacaoFaturaItemPreview(
                i.DataTransacao,
                i.Descricao,
                i.Valor,
                jaImportadasSet.Contains(chaveCompleta),
                chave, i.DataVencimentoFatura, i.NumeroParcela, i.QuantidadeParcelas);
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

        // Infere a forma de pagamento: usa a informada, depois a mais recente do cartão, depois qualquer EhCartao
        Guid? formaPagamentoId = request.FormaPagamentoId;
        if (formaPagamentoId is null)
        {
            var recenteFp = await db.ContasPagar
                .AsNoTracking()
                .Where(cp => cp.CartaoId == request.CartaoId)
                .OrderByDescending(cp => cp.DataEmissao)
                .Select(cp => (Guid?)cp.FormaPagamentoId)
                .FirstOrDefaultAsync(cancellationToken);

            formaPagamentoId = recenteFp
                ?? await db.FormasPagamento
                    .AsNoTracking()
                    .Where(f => f.FamiliaId == familiaId && f.EhCartao && f.Ativo)
                    .Select(f => (Guid?)f.Id)
                    .FirstOrDefaultAsync(cancellationToken);
        }

        if (formaPagamentoId is null)
            throw ValidationExceptionFactory.Create("FormaPagamentoId", "Nenhuma forma de pagamento de cartao encontrada para esta familia.");

        var recebedorValido = await db.Pessoas
            .AsNoTracking()
            .AnyAsync(
                p => p.Id == request.RecebedorPadraoId && p.FamiliaId == familiaId && p.Ativo,
                cancellationToken);

        if (!recebedorValido)
            throw ValidationExceptionFactory.Create("RecebedorPadraoId", "Recebedor padrao nao encontrado.");

        // Infere a conta gerencial padrão: usa a informada, depois a primeira folha de Despesa disponível
        var contaGerencialPadraoId = request.ContaGerencialPadraoId
            ?? await db.ContasGerenciais
                .AsNoTracking()
                .Where(c => c.FamiliaId == familiaId
                         && c.Ativo
                         && c.Tipo == TipoContaGerencial.Despesa
                         && !db.ContasGerenciais.Any(child => child.ContaPaiId == c.Id))
                .Select(c => (Guid?)c.Id)
                .FirstOrDefaultAsync(cancellationToken);

        if (contaGerencialPadraoId is null)
            throw ValidationExceptionFactory.Create("ContaGerencialPadraoId", "Nenhuma conta gerencial de despesa encontrada para esta familia.");

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

            var categoriaId = item.ContaGerencialId ?? contaGerencialPadraoId!.Value;

            if (item.NumeroParcela < 1 || item.QuantidadeParcelas < item.NumeroParcela || item.QuantidadeParcelas > 120)
                throw ValidationExceptionFactory.Create("Itens", "Número de parcela inválido.");
            if (item.DataVencimentoFatura.HasValue && item.DataVencimentoFatura.Value < item.DataTransacao)
                throw ValidationExceptionFactory.Create("Itens", "Vencimento da fatura anterior à compra.");
            var vencimento = item.DataVencimentoFatura ?? FaturaCartaoCompetencia.Calcular(
                item.DataTransacao, cartao.DiaFechamentoFatura, cartao.DiaVencimentoFatura).DataVencimento;
            var parcelas = new[] { ContaPagar.Criar(
                numeroDocumento: null,
                dataEmissao: item.DataTransacao,
                responsavelCompraId: null,
                recebedorId: request.RecebedorPadraoId,
                dataVencimento: vencimento,
                contaBancariaId: null,
                formaPagamentoId: formaPagamentoId.Value,
                cartaoId: request.CartaoId,
                valorOriginal: item.Valor,
                valorDesconto: 0m,
                valorJuros: 0m,
                valorMulta: 0m,
                quantidadeParcelas: item.QuantidadeParcelas,
                numeroParcela: item.NumeroParcela,
                grupoParcelamentoId: null,
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
                dataCompra: item.DataTransacao) };

            foreach (var parcela in parcelas)
            {
                parcela.DefinirChaveSerieImportacaoCartao(chaveCompleta);
                parcela.AtribuirFamilia(familiaId);
                db.ContasPagar.Add(parcela);
                db.RateiosContaGerencial.AddRange(parcela.Rateios);
            }

            if (item.DataVencimentoFatura.HasValue) existentesSet.Add(chaveCompleta);
            criadas++;
        }

        if (criadas > 0)
            await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Importação de fatura: {Criadas} criadas, {Duplicadas} duplicadas (cartão {CartaoId})",
            criadas, duplicadas, request.CartaoId);

        return new ConfirmarImportacaoFaturaResponse(criadas, duplicadas);
    }

}
