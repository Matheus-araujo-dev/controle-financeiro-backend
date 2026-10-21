using ControleFinanceiro.Application.Common.Extensions;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Application.Financeiro.Faturas;
using ControleFinanceiro.Contracts.ImportacoesWhatsapp;
using ControleFinanceiro.Domain.Cadastros.Cartoes;
using ControleFinanceiro.Domain.Financeiro;
using ControleFinanceiro.Domain.ImportacoesWhatsapp;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.ImportacoesWhatsapp;

public interface IImportacaoWhatsappAprovacaoService
{
    Task<ImportacaoWhatsappDetalheResponse?> AprovarImportacaoAsync(
        Guid id,
        AprovarImportacaoWhatsappRequest? request,
        CancellationToken cancellationToken);

    Task<ImportacaoWhatsappDetalheResponse?> CompletarFechamentoFaturaAsync(
        Guid id,
        AprovarImportacaoWhatsappRequest request,
        CancellationToken cancellationToken);

    Task<ImportacaoWhatsappDetalheResponse?> ReabrirImportacaoAsync(
        Guid id,
        CancellationToken cancellationToken);
}

public sealed class ImportacaoWhatsappAprovacaoService(
    IAppDbContext dbContext,
    FaturaCartaoAppService faturaCartaoAppService,
    IImportacaoWhatsappQueryService queryService,
    ImportacaoWhatsappSharedHelper helper) : IImportacaoWhatsappAprovacaoService
{
    private const int CompetenciasProjetadasParaCompraRecorrenteImportada = 12;

    public async Task<ImportacaoWhatsappDetalheResponse?> AprovarImportacaoAsync(
        Guid id,
        AprovarImportacaoWhatsappRequest? request,
        CancellationToken cancellationToken)
    {
        var importacao = await helper.CarregarImportacaoAsync(id, cancellationToken);
        if (importacao is null)
        {
            return null;
        }

        try
        {
            await MaterializarFaturaCartaoAprovadaAsync(importacao, request, cancellationToken);
            importacao.AprovarRevisao();
        }
        catch (InvalidOperationException exception)
        {
            throw ValidationExceptionFactory.Create("Status", exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await queryService.ObterPorIdAsync(id, cancellationToken);
    }

    public async Task<ImportacaoWhatsappDetalheResponse?> CompletarFechamentoFaturaAsync(
        Guid id,
        AprovarImportacaoWhatsappRequest request,
        CancellationToken cancellationToken)
    {
        var importacao = await helper.CarregarImportacaoAsync(id, cancellationToken);
        if (importacao is null)
        {
            return null;
        }

        if (importacao.Status != StatusImportacaoWhatsapp.Confirmado)
        {
            throw ValidationExceptionFactory.Create(
                "Status",
                "A importação precisa estar aprovada para completar o fechamento da fatura.");
        }

        var itensCompraCartao = importacao.Itens
            .Where(x => x.TipoSugestao == TipoSugestaoImportacaoWhatsapp.CompraCartao &&
                        x.Status == StatusItemImportadoWhatsapp.Confirmado)
            .ToArray();

        if (itensCompraCartao.Length == 0)
        {
            throw ValidationExceptionFactory.Create(
                "Status",
                "Não há itens confirmados de compra em cartão para materializar a fatura.");
        }

        if (await helper.ImportacaoJaPossuiGeracaoFinanceiraAsync(importacao.Id, cancellationToken))
        {
            await helper.RemoverGeracaoFinanceiraDaImportacaoAsync(importacao.Id, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await MaterializarFaturaCartaoAprovadaAsync(importacao, request, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await queryService.ObterPorIdAsync(id, cancellationToken);
    }

    public async Task<ImportacaoWhatsappDetalheResponse?> ReabrirImportacaoAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var importacao = await helper.CarregarImportacaoAsync(id, cancellationToken);
        if (importacao is null)
        {
            return null;
        }

        try
        {
            await helper.RemoverGeracaoFinanceiraDaImportacaoAsync(importacao.Id, cancellationToken);
            importacao.ReabrirRevisao();
        }
        catch (InvalidOperationException exception)
        {
            throw ValidationExceptionFactory.Create("Status", exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await faturaCartaoAppService.SincronizarAsync(cancellationToken);
        return await queryService.ObterPorIdAsync(id, cancellationToken);
    }

    private async Task MaterializarFaturaCartaoAprovadaAsync(
        ImportacaoWhatsapp importacao,
        AprovarImportacaoWhatsappRequest? request,
        CancellationToken cancellationToken)
    {
        var itensCompraCartao = importacao.Itens
            .Where(x => x.TipoSugestao == TipoSugestaoImportacaoWhatsapp.CompraCartao &&
                        x.Status == StatusItemImportadoWhatsapp.Confirmado)
            .ToArray();

        if (itensCompraCartao.Length == 0)
        {
            return;
        }

        var contexto = await ValidarAprovacaoFaturaAsync(request, cancellationToken);
        var materializacao = await MaterializarComprasCartaoImportadasAsync(importacao.Id, itensCompraCartao, contexto, cancellationToken);

        if (materializacao.ContasGeradas.Count > 0)
        {
            dbContext.ContasPagar.AddRange(materializacao.ContasGeradas);
            dbContext.RateiosContaGerencial.AddRange(materializacao.ContasGeradas.SelectMany(x => x.Rateios));

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await faturaCartaoAppService.SincronizarAsync(cancellationToken);
        await RemoverContasPagarDeFaturaInvalidasAsync(importacao.Id, materializacao.ChavesContaPagarFatura, cancellationToken);
        await CriarOuAtualizarContasPagarDeFaturaAsync(importacao.Id, materializacao.ChavesContaPagarFatura, contexto, cancellationToken);
    }

    private async Task<ImportacaoMaterializadaResult> MaterializarComprasCartaoImportadasAsync(
        Guid importacaoId,
        IReadOnlyCollection<ItemImportadoWhatsapp> itens,
        AprovacaoFaturaContext contexto,
        CancellationToken cancellationToken)
    {
        return await MaterializarComprasCartaoImportadasPorCompetenciaAtualAsync(
            importacaoId,
            itens,
            contexto,
            cancellationToken);
    }

    private async Task<ImportacaoMaterializadaResult> MaterializarComprasCartaoImportadasPorCompetenciaAtualAsync(
        Guid importacaoId,
        IReadOnlyCollection<ItemImportadoWhatsapp> itens,
        AprovacaoFaturaContext contexto,
        CancellationToken cancellationToken)
    {
        var itensPreparados = new List<ItemImportadoPreparado>(itens.Count);
        var contasGeradas = new List<ContaPagar>();
        var chavesAfetadas = new HashSet<FaturaKey>();
        var chavesContaPagarFatura = new HashSet<FaturaKey>();

        foreach (var item in itens)
        {
            if (!item.ContaGerencialId.HasValue || !item.ResponsavelId.HasValue)
            {
                throw ValidationExceptionFactory.Create(
                    "Itens",
                    "Todos os itens de compra em cartão devem possuir conta gerencial e responsável antes da aprovação.");
            }

            var payload = ImportacaoWhatsappSuggestionPayload.Parse(item.PayloadSugeridoJson);
            if (!payload.Valor.HasValue || payload.Valor.Value == 0)
            {
                throw ValidationExceptionFactory.Create("Itens", "Os itens aprovados da fatura precisam ter valor diferente de zero.");
            }

            var cartao = ResolverCartaoDaCompra(payload, contexto);
            var descricao = string.IsNullOrWhiteSpace(item.DescricaoAjustada)
                ? payload.Descricao ?? "Compra em cartão importada"
                : item.DescricaoAjustada.Trim();

            var contaGerencialId = item.ContaGerencialId!.Value;
            var responsavelId = item.ResponsavelId!.Value;
            var valorItem = payload.Valor!.Value;

            itensPreparados.Add(new ItemImportadoPreparado(item, payload, cartao, descricao, contaGerencialId, responsavelId, valorItem));
        }

        var competenciaAtualPorCartao = itensPreparados
            .GroupBy(x => x.Cartao.Id)
            .ToDictionary(
                group => group.Key,
                group => ResolverCompetenciaAtualImportacao(group.Select(x => x.Payload).ToArray(), group.First().Cartao));

        foreach (var competenciaAtual in competenciaAtualPorCartao)
        {
            chavesContaPagarFatura.Add(new FaturaKey(competenciaAtual.Key, competenciaAtual.Value.Competencia));
        }

        foreach (var preparado in itensPreparados)
        {
            var item = preparado.Item;
            var payload = preparado.Payload;
            var cartao = preparado.Cartao;
            var descricao = preparado.Descricao;
            var contaGerencialId = preparado.ContaGerencialId;
            var responsavelId = preparado.ResponsavelId;
            var valorItem = preparado.Valor;
            var infoParcelamento = payload.GetParcelamentoCompraCartaoInfo();
            var competenciaImportacaoAtual = competenciaAtualPorCartao[cartao.Id];

            if (infoParcelamento is not null)
            {
                var grupoParcelamentoId = Guid.NewGuid();
                var chaveSerie = payload.BuildInstallmentSeriesKey();
                var dataCompraParcelaAtual = CriarDataCompraDaCompetencia(competenciaImportacaoAtual);

                for (var numeroParcela = infoParcelamento.NumeroParcela; numeroParcela <= infoParcelamento.QuantidadeParcelas; numeroParcela++)
                {
                    var dataCompraParcela = dataCompraParcelaAtual.AddMonths(numeroParcela - infoParcelamento.NumeroParcela);
                    var competencia = FaturaCartaoCompetencia.Calcular(
                        dataCompraParcela,
                        cartao.DiaFechamentoFatura,
                        cartao.DiaVencimentoFatura);

                    chavesAfetadas.Add(new FaturaKey(cartao.Id, competencia.Competencia));

                    if (await ParcelaImportadaJaExisteAsync(
                            cartao.Id,
                            chaveSerie,
                            numeroParcela,
                            infoParcelamento.QuantidadeParcelas,
                            cancellationToken))
                    {
                        continue;
                    }

                    var descricaoParcela = ImportacaoWhatsappSuggestionPayload.AtualizarMarcadorParcela(
                        descricao,
                        numeroParcela,
                        infoParcelamento.QuantidadeParcelas);

                    var conta = ContaPagar.Criar(
                        numeroDocumento: null,
                        dataEmissao: dataCompraParcela,
                        responsavelCompraId: responsavelId,
                        recebedorId: contexto.RecebedorFaturaId,
                        dataVencimento: competencia.DataVencimento,
                        formaPagamentoId: contexto.FormaPagamentoCartaoId,
                        cartaoId: cartao.Id,
                        contaBancariaId: null,
                        valorOriginal: valorItem,
                        valorDesconto: 0m,
                        valorJuros: 0m,
                        valorMulta: 0m,
                        quantidadeParcelas: infoParcelamento.QuantidadeParcelas,
                        numeroParcela: numeroParcela,
                        grupoParcelamentoId: grupoParcelamentoId,
                        origemCompraPlanejadaId: null,
                        descricao: descricaoParcela,
                        observacao: $"Gerada automaticamente a partir da importação {importacaoId}.",
                        statusContaId: StatusConta.PendenteId,
                        ehRecorrente: false,
                        regraRecorrenciaId: null,
                        origem: OrigemLancamento.Importacao,
                        rateios:
                        [
                            RateioPlano.CreateSigned(contaGerencialId, valorItem)
                        ]);

                    conta.VincularOrigemImportacao(importacaoId);
                    conta.DefinirChaveSerieImportacaoCartao(chaveSerie);
                    contasGeradas.Add(conta);
                }

                continue;
            }

            if (item.MarcarComoRecorrente)
            {
                var chaveSerieRecorrente = payload.BuildRecurringSeriesKey();

                for (var monthOffset = 0; monthOffset < CompetenciasProjetadasParaCompraRecorrenteImportada; monthOffset++)
                {
                    var dataVencimentoCompetencia = competenciaImportacaoAtual.DataVencimento.AddMonths(monthOffset);
                    var competencia = FaturaCartaoCompetencia.CalcularPorDataVencimento(
                        dataVencimentoCompetencia,
                        cartao.DiaFechamentoFatura,
                        cartao.DiaVencimentoFatura);

                    chavesAfetadas.Add(new FaturaKey(cartao.Id, competencia.Competencia));

                    if (monthOffset == 0)
                    {
                        await RemoverCompraRecorrenteProjetadaAsync(
                            cartao.Id,
                            chaveSerieRecorrente,
                            competencia.DataVencimento,
                            cancellationToken);
                    }
                    else if (await CompraRecorrenteImportadaJaExisteAsync(
                                 cartao.Id,
                                 chaveSerieRecorrente,
                                 competencia.DataVencimento,
                                 cancellationToken))
                    {
                        continue;
                    }

                    var dataCompraRecorrente = CriarDataCompraDaCompetencia(competencia);
                    var contaRecorrente = ContaPagar.Criar(
                        numeroDocumento: null,
                        dataEmissao: dataCompraRecorrente,
                        responsavelCompraId: responsavelId,
                        recebedorId: contexto.RecebedorFaturaId,
                        dataVencimento: competencia.DataVencimento,
                        formaPagamentoId: contexto.FormaPagamentoCartaoId,
                        cartaoId: cartao.Id,
                        contaBancariaId: null,
                        valorOriginal: valorItem,
                        valorDesconto: 0m,
                        valorJuros: 0m,
                        valorMulta: 0m,
                        quantidadeParcelas: 1,
                        numeroParcela: 1,
                        grupoParcelamentoId: null,
                        origemCompraPlanejadaId: null,
                        descricao: descricao,
                        observacao: $"Gerada automaticamente a partir da importacao {importacaoId}.",
                        statusContaId: StatusConta.PendenteId,
                        ehRecorrente: false,
                        regraRecorrenciaId: null,
                        origem: OrigemLancamento.Importacao,
                        rateios:
                        [
                            RateioPlano.CreateSigned(contaGerencialId, valorItem)
                        ]);

                    contaRecorrente.VincularOrigemImportacao(importacaoId);
                    contaRecorrente.DefinirChaveSerieImportacaoCartao(chaveSerieRecorrente);
                    contasGeradas.Add(contaRecorrente);
                }

                continue;
            }

            var dataCompra = payload.DataIdentificada
                ?? throw ValidationExceptionFactory.Create("Itens", $"O item '{descricao}' não possui data identificada.");
            chavesAfetadas.Add(new FaturaKey(cartao.Id, competenciaImportacaoAtual.Competencia));

            // Deduplicação com lançamentos manuais: se a compra já foi registrada à mão
            // (mesmo cartão, mesmo valor, data próxima), aproveita o lançamento existente
            // — rateio, descrição e classificação são preservados — e não duplica.
            if (await ExisteCompraManualCorrespondenteAsync(cartao.Id, valorItem, dataCompra, cancellationToken))
            {
                continue;
            }

            var contaUnica = ContaPagar.Criar(
                numeroDocumento: null,
                dataEmissao: dataCompra,
                responsavelCompraId: responsavelId,
                recebedorId: contexto.RecebedorFaturaId,
                dataVencimento: competenciaImportacaoAtual.DataVencimento,
                formaPagamentoId: contexto.FormaPagamentoCartaoId,
                cartaoId: cartao.Id,
                contaBancariaId: null,
                valorOriginal: valorItem,
                valorDesconto: 0m,
                valorJuros: 0m,
                valorMulta: 0m,
                quantidadeParcelas: 1,
                numeroParcela: 1,
                grupoParcelamentoId: null,
                origemCompraPlanejadaId: null,
                descricao: descricao,
                observacao: $"Gerada automaticamente a partir da importação {importacaoId}.",
                statusContaId: StatusConta.PendenteId,
                ehRecorrente: false,
                regraRecorrenciaId: null,
                origem: OrigemLancamento.Importacao,
                rateios:
                [
                    RateioPlano.CreateSigned(contaGerencialId, valorItem)
                ]);

            contaUnica.VincularOrigemImportacao(importacaoId);
            contasGeradas.Add(contaUnica);
        }

        return new ImportacaoMaterializadaResult(
            contasGeradas,
            chavesAfetadas.ToArray(),
            chavesContaPagarFatura.ToArray());
    }

    private async Task<bool> ExisteCompraManualCorrespondenteAsync(
        Guid cartaoId,
        decimal valor,
        DateOnly dataCompra,
        CancellationToken cancellationToken)
    {
        var dataInicial = dataCompra.AddDays(-3);
        var dataFinal = dataCompra.AddDays(3);

        return await dbContext.ContasPagar.AnyAsync(
            x => x.CartaoId == cartaoId &&
                 x.Origem == OrigemLancamento.Manual &&
                 x.OrigemImportacaoWhatsappId == null &&
                 x.ValorLiquido == valor &&
                 x.DataEmissao >= dataInicial &&
                 x.DataEmissao <= dataFinal &&
                 x.StatusContaId != StatusConta.CanceladaId,
            cancellationToken);
    }

    private static FaturaCartaoCompetencia.Resultado ResolverCompetenciaAtualImportacao(
        IReadOnlyCollection<ImportacaoWhatsappSuggestionPayload> payloads,
        Cartao cartao)
    {
        var dataVencimento = payloads.Select(x => x.DataVencimento).FirstOrDefault(x => x.HasValue);
        if (dataVencimento.HasValue)
        {
            return FaturaCartaoCompetencia.CalcularPorDataVencimento(
                dataVencimento.Value,
                cartao.DiaFechamentoFatura,
                cartao.DiaVencimentoFatura);
        }

        var periodoFim = payloads.Select(x => x.PeriodoFim).FirstOrDefault(x => x.HasValue);
        if (periodoFim.HasValue)
        {
            var anchorDate = new DateOnly(periodoFim.Value.Year, periodoFim.Value.Month, 1);
            return FaturaCartaoCompetencia.Calcular(anchorDate, cartao.DiaFechamentoFatura, cartao.DiaVencimentoFatura);
        }

        var datasSemParcela = payloads
            .Where(x => x.GetParcelamentoCompraCartaoInfo() is null)
            .Select(x => x.DataIdentificada)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToArray();

        if (datasSemParcela.Length > 0)
        {
            var ultimaDataSemParcela = datasSemParcela.Max();
            return FaturaCartaoCompetencia.Calcular(
                ultimaDataSemParcela,
                cartao.DiaFechamentoFatura,
                cartao.DiaVencimentoFatura);
        }

        var datasIdentificadas = payloads
            .Select(x => x.DataIdentificada)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToArray();

        if (datasIdentificadas.Length > 0)
        {
            var ultimaDataIdentificada = datasIdentificadas.Max();
            return FaturaCartaoCompetencia.Calcular(
                ultimaDataIdentificada,
                cartao.DiaFechamentoFatura,
                cartao.DiaVencimentoFatura);
        }

        throw ValidationExceptionFactory.Create(
            "Itens",
            "Não foi possível inferir a competência atual da fatura importada para projetar as parcelas.");
    }

    private static DateOnly CriarDataCompraDaCompetencia(FaturaCartaoCompetencia.Resultado competencia)
    {
        return new DateOnly(competencia.DataFechamento.Year, competencia.DataFechamento.Month, 1);
    }

    private async Task CriarOuAtualizarContasPagarDeFaturaAsync(
        Guid importacaoId,
        IReadOnlyCollection<FaturaKey> chavesAfetadas,
        AprovacaoFaturaContext contexto,
        CancellationToken cancellationToken)
    {
        if (chavesAfetadas.Count == 0)
        {
            return;
        }

        var faturas = await dbContext.FaturasCartao
            .WhereIn(x => x.CartaoId, chavesAfetadas.Select(chave => chave.CartaoId))
            .ToListAsync(cancellationToken);

        var cartoes = await dbContext.Cartoes
            .AsNoTracking()
            .WhereIn(x => x.Id, chavesAfetadas.Select(chave => chave.CartaoId))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var cartaoIds = chavesAfetadas.Select(chave => chave.CartaoId).ToArray();
        var contasCartao = await dbContext.ContasPagar
            .Where(x => x.CartaoId.HasValue && x.StatusContaId != StatusConta.CanceladaId)
            .WhereIn(x => x.CartaoId!.Value, cartaoIds)
            .ToListAsync(cancellationToken);

        foreach (var chave in chavesAfetadas)
        {
            var fatura = faturas.SingleOrDefault(x => x.CartaoId == chave.CartaoId && x.Competencia == chave.Competencia);
            if (fatura is null)
            {
                continue;
            }

            var cartao = cartoes[chave.CartaoId];
            var contasDaFatura = contasCartao
                .Where(x =>
                    x.CartaoId == chave.CartaoId &&
                    FaturaCartaoCompetencia.CalcularPorDataVencimento(
                        x.DataVencimento,
                        cartao.DiaFechamentoFatura,
                        cartao.DiaVencimentoFatura).Competencia == chave.Competencia)
                .ToArray();

            if (contasDaFatura.Length == 0)
            {
                continue;
            }

            var idsContasDaFatura = contasDaFatura.Select(x => x.Id).ToArray();
            var rateios = await dbContext.RateiosContaGerencial
                .Where(x => x.ContaPagarId.HasValue)
                .WhereIn(x => x.ContaPagarId!.Value, idsContasDaFatura)
                .ToArrayAsync(cancellationToken);

            var rateiosAgrupados = rateios
                .GroupBy(x => x.ContaGerencialId)
                .Select(group => new
                {
                    ContaGerencialId = group.Key,
                    Valor = decimal.Round(group.Sum(x => x.Valor), 2, MidpointRounding.AwayFromZero)
                })
                .Where(group => group.Valor != 0)
                .Select(group => RateioPlano.CreateSigned(group.ContaGerencialId, group.Valor))
                .ToArray();

            var contaPagarFatura = await dbContext.ContasPagar
                .SingleOrDefaultAsync(
                    x => x.FaturaCartaoId == fatura.Id && !x.CartaoId.HasValue && x.StatusContaId != StatusConta.CanceladaId,
                    cancellationToken);

            var descricaoFatura = $"Fatura {cartao.Nome} {fatura.Competencia}";

            if (contaPagarFatura is null)
            {
                contaPagarFatura = ContaPagar.Criar(
                    numeroDocumento: fatura.Competencia,
                    dataEmissao: fatura.DataFechamento,
                    responsavelCompraId: contexto.ResponsavelPagamentoFaturaId,
                    recebedorId: contexto.RecebedorFaturaId,
                    dataVencimento: fatura.DataVencimento,
                    formaPagamentoId: contexto.FormaPagamentoCartaoId,
                    cartaoId: null,
                    contaBancariaId: null,
                    valorOriginal: fatura.ValorTotal,
                    valorDesconto: 0m,
                    valorJuros: 0m,
                    valorMulta: 0m,
                    quantidadeParcelas: 1,
                    numeroParcela: 1,
                    grupoParcelamentoId: null,
                    origemCompraPlanejadaId: null,
                    descricao: descricaoFatura,
                    observacao: $"Gerada automaticamente a partir da importação {importacaoId}.",
                    statusContaId: StatusConta.PendenteId,
                    ehRecorrente: false,
                    regraRecorrenciaId: null,
                    origem: OrigemLancamento.Importacao,
                    rateios: rateiosAgrupados);

                contaPagarFatura.VincularOrigemImportacao(importacaoId);
                contaPagarFatura.VincularFaturaCartao(fatura.Id);

                dbContext.ContasPagar.Add(contaPagarFatura);
                dbContext.RateiosContaGerencial.AddRange(contaPagarFatura.Rateios);
            }
            else if (contaPagarFatura.StatusContaId != StatusConta.LiquidadaId)
            {
                contaPagarFatura.Atualizar(
                    numeroDocumento: fatura.Competencia,
                    dataEmissao: fatura.DataFechamento,
                    responsavelCompraId: contexto.ResponsavelPagamentoFaturaId,
                    recebedorId: contexto.RecebedorFaturaId,
                    dataVencimento: fatura.DataVencimento,
                    formaPagamentoId: contexto.FormaPagamentoCartaoId,
                    cartaoId: null,
                    contaBancariaId: null,
                    valorOriginal: fatura.ValorTotal,
                    valorDesconto: 0m,
                    valorJuros: 0m,
                    valorMulta: 0m,
                    descricao: descricaoFatura,
                    observacao: contaPagarFatura.Observacao,
                    statusContaId: contaPagarFatura.StatusContaId,
                    rateios: rateiosAgrupados);

                contaPagarFatura.VincularFaturaCartao(fatura.Id);
                await SincronizarRateiosContaPagarAsync(contaPagarFatura.Id, contaPagarFatura.Rateios, cancellationToken);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task RemoverContasPagarDeFaturaInvalidasAsync(
        Guid importacaoId,
        IReadOnlyCollection<FaturaKey> chavesValidas,
        CancellationToken cancellationToken)
    {
        var chavesValidasSet = chavesValidas.ToHashSet();

        var contasFaturaExistentes = await (
            from conta in dbContext.ContasPagar
            join fatura in dbContext.FaturasCartao on conta.FaturaCartaoId!.Value equals fatura.Id
            where conta.OrigemImportacaoWhatsappId == importacaoId &&
                  conta.FaturaCartaoId.HasValue &&
                  !conta.CartaoId.HasValue &&
                  conta.StatusContaId != StatusConta.CanceladaId
            select new
            {
                Conta = conta,
                Chave = new FaturaKey(fatura.CartaoId, fatura.Competencia)
            })
            .ToListAsync(cancellationToken);

        var contasInvalidas = contasFaturaExistentes
            .Where(x => !chavesValidasSet.Contains(x.Chave))
            .Select(x => x.Conta)
            .ToArray();

        if (contasInvalidas.Length == 0)
        {
            return;
        }

        var contaIds = contasInvalidas.Select(x => x.Id).ToArray();
        var rateios = await dbContext.RateiosContaGerencial
            .Where(x => x.ContaPagarId.HasValue)
            .WhereIn(x => x.ContaPagarId!.Value, contaIds)
            .ToListAsync(cancellationToken);

        dbContext.RateiosContaGerencial.RemoveRange(rateios);
        dbContext.ContasPagar.RemoveRange(contasInvalidas);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> ParcelaImportadaJaExisteAsync(
        Guid cartaoId,
        string? chaveSerie,
        int numeroParcela,
        int quantidadeParcelas,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(chaveSerie))
        {
            return false;
        }

        return await dbContext.ContasPagar.AnyAsync(
            x => x.CartaoId == cartaoId &&
                 x.ChaveSerieImportacaoCartao == chaveSerie &&
                 x.NumeroParcela == numeroParcela &&
                 x.QuantidadeParcelas == quantidadeParcelas &&
                 x.StatusContaId != StatusConta.CanceladaId,
            cancellationToken);
    }

    private async Task<bool> CompraRecorrenteImportadaJaExisteAsync(
        Guid cartaoId,
        string? chaveSerie,
        DateOnly dataVencimento,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(chaveSerie))
        {
            return false;
        }

        return await dbContext.ContasPagar.AnyAsync(
            x => x.CartaoId == cartaoId &&
                 x.ChaveSerieImportacaoCartao == chaveSerie &&
                 x.DataVencimento == dataVencimento &&
                 x.StatusContaId != StatusConta.CanceladaId,
            cancellationToken);
    }

    private async Task RemoverCompraRecorrenteProjetadaAsync(
        Guid cartaoId,
        string? chaveSerie,
        DateOnly dataVencimento,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(chaveSerie))
        {
            return;
        }

        var contas = await dbContext.ContasPagar
            .Where(x => x.CartaoId == cartaoId &&
                        x.ChaveSerieImportacaoCartao == chaveSerie &&
                        x.DataVencimento == dataVencimento &&
                        x.StatusContaId != StatusConta.CanceladaId)
            .ToListAsync(cancellationToken);

        if (contas.Count == 0)
        {
            return;
        }

        var contaIds = contas.Select(x => x.Id).ToArray();
        var rateios = await dbContext.RateiosContaGerencial
            .Where(x => x.ContaPagarId.HasValue)
            .WhereIn(x => x.ContaPagarId!.Value, contaIds)
            .ToListAsync(cancellationToken);

        dbContext.RateiosContaGerencial.RemoveRange(rateios);
        dbContext.ContasPagar.RemoveRange(contas);
    }

    private async Task SincronizarRateiosContaPagarAsync(
        Guid contaPagarId,
        IReadOnlyCollection<RateioContaGerencial> novosRateios,
        CancellationToken cancellationToken)
    {
        var existentes = await dbContext.RateiosContaGerencial
            .Where(x => x.ContaPagarId == contaPagarId)
            .ToListAsync(cancellationToken);

        dbContext.RateiosContaGerencial.RemoveRange(existentes);
        dbContext.RateiosContaGerencial.AddRange(novosRateios);
    }

    private async Task<AprovacaoFaturaContext> ValidarAprovacaoFaturaAsync(
        AprovarImportacaoWhatsappRequest? request,
        CancellationToken cancellationToken)
    {
        if (request?.RecebedorFaturaId is null || request.RecebedorFaturaId == Guid.Empty)
        {
            throw ValidationExceptionFactory.Create("RecebedorFaturaId", "Recebedor da fatura é obrigatório.");
        }

        if (request.ResponsavelPagamentoFaturaId is null || request.ResponsavelPagamentoFaturaId == Guid.Empty)
        {
            throw ValidationExceptionFactory.Create("ResponsavelPagamentoFaturaId", "Responsável pelo pagamento da fatura é obrigatório.");
        }

        var cartaoIds = request.CartaoIds?
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray() ?? [];

        if (cartaoIds.Length == 0)
        {
            throw ValidationExceptionFactory.Create("CartaoIds", "Informe ao menos um cartão vinculado à fatura.");
        }

        if (!await dbContext.Pessoas.AnyAsync(x => x.Id == request.RecebedorFaturaId.Value, cancellationToken))
        {
            throw ValidationExceptionFactory.Create("RecebedorFaturaId", "Recebedor da fatura não encontrado.");
        }

        if (!await dbContext.Pessoas.AnyAsync(x => x.Id == request.ResponsavelPagamentoFaturaId.Value, cancellationToken))
        {
            throw ValidationExceptionFactory.Create("ResponsavelPagamentoFaturaId", "Responsável pelo pagamento não encontrado.");
        }

        var cartoes = await dbContext.Cartoes
            .AsNoTracking()
            .Where(x => cartaoIds.Contains(x.Id) && x.Ativo)
            .ToArrayAsync(cancellationToken);

        if (cartoes.Length != cartaoIds.Length)
        {
            throw ValidationExceptionFactory.Create("CartaoIds", "Um ou mais cartões vinculados não foram encontrados.");
        }

        var formaPagamentoCartao = await dbContext.FormasPagamento
            .AsNoTracking()
            .Where(x => x.Ativo && x.EhCartao)
            .OrderBy(x => x.Nome)
            .FirstOrDefaultAsync(cancellationToken);

        if (formaPagamentoCartao is null)
        {
            throw ValidationExceptionFactory.Create("CartaoIds", "Cadastre uma forma de pagamento ativa do tipo cartão.");
        }

        return new AprovacaoFaturaContext(
            request.RecebedorFaturaId.Value,
            request.ResponsavelPagamentoFaturaId.Value,
            formaPagamentoCartao.Id,
            cartoes.ToDictionary(x => x.Id));
    }

    private Cartao ResolverCartaoDaCompra(
        ImportacaoWhatsappSuggestionPayload payload,
        AprovacaoFaturaContext contexto)
    {
        if (!string.IsNullOrWhiteSpace(payload.CartaoFinal))
        {
            var matches = contexto.Cartoes.Values
                .Where(x => x.NumeroFinal == payload.CartaoFinal)
                .ToArray();

            if (matches.Length == 1)
            {
                return matches[0];
            }

            if (matches.Length > 1)
            {
                throw ValidationExceptionFactory.Create("CartaoIds", $"Mais de um cartão selecionado termina com {payload.CartaoFinal}.");
            }
        }

        if (contexto.Cartoes.Count == 1)
        {
            return contexto.Cartoes.Values.Single();
        }

        throw ValidationExceptionFactory.Create(
            "CartaoIds",
            "Não foi possível identificar automaticamente o cartão do item importado. Ajuste os cartões vinculados da fatura.");
    }
}
