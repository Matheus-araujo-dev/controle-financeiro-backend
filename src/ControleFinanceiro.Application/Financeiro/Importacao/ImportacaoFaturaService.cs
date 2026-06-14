using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Contracts.Financeiro.ImportacaoFatura;
using ControleFinanceiro.Domain.Cadastros.Cartoes;
using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ControleFinanceiro.Application.Financeiro.Importacao;

public sealed class ImportacaoFaturaService(IAppDbContext db, ILogger<ImportacaoFaturaService> logger)
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
            parseResult = PdfFaturaParser.Parse(arquivoStream);
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
                    RateioPlano.CreateSigned(request.ContaGerencialPadraoId, item.Valor)
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
}
