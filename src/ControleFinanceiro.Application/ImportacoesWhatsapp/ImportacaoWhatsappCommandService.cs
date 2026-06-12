using ControleFinanceiro.Application.Common.Exceptions;
using ControleFinanceiro.Application.Common.Extensions;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Application.Financeiro.Faturas;
using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Contracts.ImportacoesWhatsapp;
using ControleFinanceiro.Domain.Cadastros.Cartoes;
using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;
using ControleFinanceiro.Domain.Cadastros.FormasPagamento;
using ControleFinanceiro.Domain.Financeiro;
using ControleFinanceiro.Application.Identidade;
using ControleFinanceiro.Domain.ImportacoesWhatsapp;
using ControleFinanceiro.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ControleFinanceiro.Application.ImportacoesWhatsapp;

public interface IImportacaoWhatsappCommandService
{
    Task<ImportacaoWhatsappDetalheResponse> ReceberWebhookAsync(
        ReceberImportacaoWhatsappWebhookRequest request,
        CancellationToken cancellationToken);

    Task<ImportacaoWhatsappDetalheResponse?> ReprocessarAsync(
        Guid id,
        CancellationToken cancellationToken);

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

    Task<ImportacaoWhatsappDetalheResponse?> ConfirmarItemAsync(
        Guid itemId,
        RevisarItemImportadoWhatsappRequest request,
        CancellationToken cancellationToken);

    Task<ImportacaoWhatsappDetalheResponse?> RejeitarItemAsync(
        Guid itemId,
        RevisarItemImportadoWhatsappRequest request,
        CancellationToken cancellationToken);
}

public sealed class ImportacaoWhatsappCommandService(
    IAppDbContext dbContext,
    IFileStorage fileStorage,
    IDocumentExtractor documentExtractor,
    IImportSuggestionService importSuggestionService,
    FaturaCartaoAppService faturaCartaoAppService,
    IImportacaoWhatsappQueryService queryService,
    ICurrentUser currentUser,
    IOptions<IdentidadeOptions> identidadeOptions) : IImportacaoWhatsappCommandService
{
    private const int CompetenciasProjetadasParaCompraRecorrenteImportada = 12;

    private static readonly string[] MimeTypesSuportados =
    [
        "application/pdf",
        "image/jpeg",
        "image/png",
        "image/webp",
        "text/plain"
    ];

    public async Task<ImportacaoWhatsappDetalheResponse> ReceberWebhookAsync(
        ReceberImportacaoWhatsappWebhookRequest request,
        CancellationToken cancellationToken)
    {
        ValidarWebhook(request);

        // Webhook é anônimo: associa a importação à família padrão configurada.
        // TODO (fase WhatsApp): resolver a família pelo remetente cadastrado.
        if (currentUser.FamiliaId is null && identidadeOptions.Value.FamiliaPadraoId is { } familiaPadraoId)
        {
            dbContext.DefinirFamiliaCorrente(familiaPadraoId);
        }

        var tipoOrigem = MapearTipoOrigem(request.TipoOrigem);
        var importacao = ImportacaoWhatsapp.CriarRecebida(
            tipoOrigem,
            request.Remetente,
            request.TextoBruto,
            request.NomeArquivo,
            null,
            request.MimeType);

        if (!string.IsNullOrWhiteSpace(request.ArquivoBase64))
        {
            try
            {
                var fileStorageResult = await fileStorage.SaveAsync(
                    new FileStorageRequest(
                        importacao.Id,
                        request.NomeArquivo!,
                        request.MimeType!,
                        request.ArquivoBase64),
                    cancellationToken);
                importacao.RegistrarArtefatoArmazenado(fileStorageResult.CaminhoArquivo);
            }
            catch (ArgumentException exception)
            {
                throw ValidationExceptionFactory.Create("ArquivoBase64", exception.Message);
            }
        }

        dbContext.ImportacoesWhatsapp.Add(importacao);
        await ProcessarImportacaoAsync(importacao, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await queryService.ObterPorIdAsync(importacao.Id, cancellationToken)
            ?? throw new InvalidOperationException("Falha ao recuperar a importaÃ§Ã£o processada.");
    }

    public async Task<ImportacaoWhatsappDetalheResponse?> ReprocessarAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var importacao = await CarregarImportacaoAsync(id, cancellationToken);
        if (importacao is null)
        {
            return null;
        }

        if (importacao.Status == StatusImportacaoWhatsapp.Confirmado)
        {
            throw ValidationExceptionFactory.Create("Status", "Reabra a importaÃ§Ã£o antes de reprocessar.");
        }

        if (importacao.Itens.Count > 0)
        {
            dbContext.ItensImportadosWhatsapp.RemoveRange(importacao.Itens);
            importacao.SubstituirItens([]);
        }

        await ProcessarImportacaoAsync(importacao, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await queryService.ObterPorIdAsync(id, cancellationToken);
    }

    public async Task<ImportacaoWhatsappDetalheResponse?> AprovarImportacaoAsync(
        Guid id,
        AprovarImportacaoWhatsappRequest? request,
        CancellationToken cancellationToken)
    {
        var importacao = await CarregarImportacaoAsync(id, cancellationToken);
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

    public async Task<ImportacaoWhatsappDetalheResponse?> ReabrirImportacaoAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var importacao = await CarregarImportacaoAsync(id, cancellationToken);
        if (importacao is null)
        {
            return null;
        }

        try
        {
            await RemoverGeracaoFinanceiraDaImportacaoAsync(importacao.Id, cancellationToken);
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

    public async Task<ImportacaoWhatsappDetalheResponse?> CompletarFechamentoFaturaAsync(
        Guid id,
        AprovarImportacaoWhatsappRequest request,
        CancellationToken cancellationToken)
    {
        var importacao = await CarregarImportacaoAsync(id, cancellationToken);
        if (importacao is null)
        {
            return null;
        }

        if (importacao.Status != StatusImportacaoWhatsapp.Confirmado)
        {
            throw ValidationExceptionFactory.Create(
                "Status",
                "A importaÃ§Ã£o precisa estar aprovada para completar o fechamento da fatura.");
        }

        var itensCompraCartao = importacao.Itens
            .Where(x => x.TipoSugestao == TipoSugestaoImportacaoWhatsapp.CompraCartao &&
                        x.Status == StatusItemImportadoWhatsapp.Confirmado)
            .ToArray();

        if (itensCompraCartao.Length == 0)
        {
            throw ValidationExceptionFactory.Create(
                "Status",
                "NÃ£o hÃ¡ itens confirmados de compra em cartÃ£o para materializar a fatura.");
        }

        if (await ImportacaoJaPossuiGeracaoFinanceiraAsync(importacao.Id, cancellationToken))
        {
            await RemoverGeracaoFinanceiraDaImportacaoAsync(importacao.Id, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await MaterializarFaturaCartaoAprovadaAsync(importacao, request, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await queryService.ObterPorIdAsync(id, cancellationToken);
    }

    public async Task<ImportacaoWhatsappDetalheResponse?> ConfirmarItemAsync(
        Guid itemId,
        RevisarItemImportadoWhatsappRequest request,
        CancellationToken cancellationToken)
    {
        var importacao = await CarregarImportacaoPorItemAsync(itemId, cancellationToken);
        if (importacao is null)
        {
            return null;
        }

        var item = importacao.Itens.Single(x => x.Id == itemId);
        var confirmacaoClassificada = await ValidarEClassificarConfirmacaoAsync(item, request, cancellationToken);

        try
        {
            ValidarImportacaoNaoAprovada(importacao);

            if (item.Status == StatusItemImportadoWhatsapp.Sugerido)
            {
                item.Confirmar(
                    request.Observacao,
                    confirmacaoClassificada.DescricaoAjustada,
                    request.ContaGerencialId,
                    request.ResponsavelId,
                    confirmacaoClassificada.ContaReceberId,
                    confirmacaoClassificada.MarcarComoRecorrente);
            }
            else if (item.Status == StatusItemImportadoWhatsapp.Confirmado)
            {
                item.AtualizarConfirmacao(
                    request.Observacao,
                    confirmacaoClassificada.DescricaoAjustada,
                    request.ContaGerencialId,
                    request.ResponsavelId,
                    confirmacaoClassificada.ContaReceberId,
                    confirmacaoClassificada.MarcarComoRecorrente);
            }
            else if (item.Status == StatusItemImportadoWhatsapp.Rejeitado)
            {
                item.ReabrirParaEdicao();
                item.Confirmar(
                    request.Observacao,
                    confirmacaoClassificada.DescricaoAjustada,
                    request.ContaGerencialId,
                    request.ResponsavelId,
                    confirmacaoClassificada.ContaReceberId,
                    confirmacaoClassificada.MarcarComoRecorrente);
            }
            else
            {
                throw new InvalidOperationException("Somente itens da importaÃ§Ã£o em revisÃ£o podem ser confirmados.");
            }

            importacao.AtualizarStatusRevisao();
        }
        catch (InvalidOperationException exception)
        {
            throw ValidationExceptionFactory.Create("Status", exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await queryService.ObterPorIdAsync(importacao.Id, cancellationToken);
    }

    public async Task<ImportacaoWhatsappDetalheResponse?> RejeitarItemAsync(
        Guid itemId,
        RevisarItemImportadoWhatsappRequest request,
        CancellationToken cancellationToken)
    {
        var importacao = await CarregarImportacaoPorItemAsync(itemId, cancellationToken);
        if (importacao is null)
        {
            return null;
        }

        var item = importacao.Itens.Single(x => x.Id == itemId);
        try
        {
            ValidarImportacaoNaoAprovada(importacao);

            if (item.Status != StatusItemImportadoWhatsapp.Sugerido)
            {
                item.ReabrirParaEdicao();
            }

            item.Rejeitar(request.Observacao);
            importacao.AtualizarStatusRevisao();
        }
        catch (InvalidOperationException exception)
        {
            throw ValidationExceptionFactory.Create("Status", exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await queryService.ObterPorIdAsync(importacao.Id, cancellationToken);
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

#pragma warning disable CS0162
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
                    "Todos os itens de compra em cartÃƒÂ£o devem possuir conta gerencial e responsÃƒÂ¡vel antes da aprovaÃƒÂ§ÃƒÂ£o.");
            }

            var payload = ImportacaoWhatsappSuggestionPayload.Parse(item.PayloadSugeridoJson);
            if (!payload.Valor.HasValue || payload.Valor.Value == 0)
            {
                throw ValidationExceptionFactory.Create("Itens", "Os itens aprovados da fatura precisam ter valor diferente de zero.");
            }

            var cartao = ResolverCartaoDaCompra(payload, contexto);
            var descricao = string.IsNullOrWhiteSpace(item.DescricaoAjustada)
                ? payload.Descricao ?? "Compra em cartÃƒÂ£o importada"
                : item.DescricaoAjustada.Trim();

            var contaGerencialId = item.ContaGerencialId!.Value;
            var responsavelId = item.ResponsavelId!.Value;
            var valorItem = payload.Valor!.Value;
            var competenciaImportacaoAtual = payload.DataVencimento.HasValue
                ? FaturaCartaoCompetencia.CalcularPorDataVencimento(
                    payload.DataVencimento.Value,
                    cartao.DiaFechamentoFatura,
                    cartao.DiaVencimentoFatura)
                : (payload.DataIdentificada.HasValue
                    ? FaturaCartaoCompetencia.Calcular(
                        payload.DataIdentificada.Value,
                        cartao.DiaFechamentoFatura,
                        cartao.DiaVencimentoFatura)
                    : FaturaCartaoCompetencia.Calcular(
                        new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1),
                        cartao.DiaFechamentoFatura,
                        cartao.DiaVencimentoFatura));

            var infoParcelamento = payload.GetParcelamentoCompraCartaoInfo();
            if (infoParcelamento is not null)
            {
                if (!payload.DataIdentificada.HasValue)
                {
                    throw ValidationExceptionFactory.Create(
                        "Itens",
                        $"O item '{descricao}' nÃƒÂ£o possui data identificada para projetar as parcelas futuras.");
                }

                var grupoParcelamentoId = Guid.NewGuid();
                var chaveSerie = payload.BuildInstallmentSeriesKey();
                var dataParcelaAtual = payload.DataIdentificada.Value;

                for (var numeroParcela = infoParcelamento.NumeroParcela; numeroParcela <= infoParcelamento.QuantidadeParcelas; numeroParcela++)
                {
                    var dataCompraParcela = dataParcelaAtual.AddMonths(numeroParcela - infoParcelamento.NumeroParcela);
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
                        responsavelCompraId: item.ResponsavelId,
                        recebedorId: contexto.RecebedorFaturaId,
                        dataVencimento: competencia.DataVencimento,
                        formaPagamentoId: contexto.FormaPagamentoCartaoId,
                        cartaoId: cartao.Id,
                        contaBancariaId: null,
                        valorOriginal: payload.Valor.Value,
                        valorDesconto: 0m,
                        valorJuros: 0m,
                        valorMulta: 0m,
                        quantidadeParcelas: infoParcelamento.QuantidadeParcelas,
                        numeroParcela: numeroParcela,
                        grupoParcelamentoId: grupoParcelamentoId,
                        origemCompraPlanejadaId: null,
                        descricao: descricao,
                        observacao: $"Gerada automaticamente a partir da importaÃƒÂ§ÃƒÂ£o {importacaoId}.",
                        statusContaId: StatusConta.PendenteId,
                        ehRecorrente: false,
                        regraRecorrenciaId: null,
                        origem: OrigemLancamento.Importacao,
                        rateios:
                        [
                            RateioPlano.CreateSigned(item.ContaGerencialId.Value, payload.Valor.Value)
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

                    var dataCompraRecorrente = monthOffset == 0
                        ? payload.DataIdentificada ?? CriarDataCompraDaCompetencia(competencia)
                        : CriarDataCompraDaCompetencia(competencia);

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
                        observacao: $"Gerada automaticamente a partir da importaÃ§Ã£o {importacaoId}.",
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

                    var dataCompraRecorrente = monthOffset == 0
                        ? payload.DataIdentificada ?? CriarDataCompraDaCompetencia(competencia)
                        : CriarDataCompraDaCompetencia(competencia);

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
                        observacao: $"Gerada automaticamente a partir da importaÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â§ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â£o {importacaoId}.",
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

                    var dataCompraRecorrente = monthOffset == 0
                        ? payload.DataIdentificada ?? CriarDataCompraDaCompetencia(competencia)
                        : CriarDataCompraDaCompetencia(competencia);

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
                        observacao: $"Gerada automaticamente a partir da importaÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â§ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â£o {importacaoId}.",
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

                    var dataCompraRecorrente = monthOffset == 0
                        ? payload.DataIdentificada ?? CriarDataCompraDaCompetencia(competencia)
                        : CriarDataCompraDaCompetencia(competencia);

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
                        observacao: $"Gerada automaticamente a partir da importaÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â§ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â£o {importacaoId}.",
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

                    var dataCompraRecorrente = monthOffset == 0
                        ? payload.DataIdentificada ?? CriarDataCompraDaCompetencia(competencia)
                        : CriarDataCompraDaCompetencia(competencia);

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
                        observacao: $"Gerada automaticamente a partir da importaÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â§ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â£o {importacaoId}.",
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

                    var dataCompraRecorrente = monthOffset == 0
                        ? payload.DataIdentificada ?? CriarDataCompraDaCompetencia(competencia)
                        : CriarDataCompraDaCompetencia(competencia);

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
                        observacao: $"Gerada automaticamente a partir da importaÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â§ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â£o {importacaoId}.",
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
                        observacao: $"Gerada automaticamente a partir da importaÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â§ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â£o {importacaoId}.",
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
                ?? throw ValidationExceptionFactory.Create("Itens", $"O item '{descricao}' nÃƒÂ£o possui data identificada.");
            chavesAfetadas.Add(new FaturaKey(cartao.Id, competenciaImportacaoAtual.Competencia));

            var contaUnica = ContaPagar.Criar(
                numeroDocumento: null,
                dataEmissao: dataCompra,
                responsavelCompraId: item.ResponsavelId,
                recebedorId: contexto.RecebedorFaturaId,
                dataVencimento: competenciaImportacaoAtual.DataVencimento,
                formaPagamentoId: contexto.FormaPagamentoCartaoId,
                cartaoId: cartao.Id,
                contaBancariaId: null,
                valorOriginal: payload.Valor.Value,
                valorDesconto: 0m,
                valorJuros: 0m,
                valorMulta: 0m,
                quantidadeParcelas: 1,
                numeroParcela: 1,
                grupoParcelamentoId: null,
                origemCompraPlanejadaId: null,
                descricao: descricao,
                observacao: $"Gerada automaticamente a partir da importaÃƒÂ§ÃƒÂ£o {importacaoId}.",
                statusContaId: StatusConta.PendenteId,
                ehRecorrente: false,
                regraRecorrenciaId: null,
                origem: OrigemLancamento.Importacao,
                rateios:
                [
                    RateioPlano.CreateSigned(item.ContaGerencialId.Value, payload.Valor.Value)
                ]);

            contaUnica.VincularOrigemImportacao(importacaoId);
            contasGeradas.Add(contaUnica);
        }

        return new ImportacaoMaterializadaResult(
            contasGeradas,
            chavesAfetadas.ToArray(),
            chavesAfetadas.ToArray());
#pragma warning restore CS0162
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
                    "Todos os itens de compra em cartÃƒÆ’Ã‚Â£o devem possuir conta gerencial e responsÃƒÆ’Ã‚Â¡vel antes da aprovaÃƒÆ’Ã‚Â§ÃƒÆ’Ã‚Â£o.");
            }

            var payload = ImportacaoWhatsappSuggestionPayload.Parse(item.PayloadSugeridoJson);
            if (!payload.Valor.HasValue || payload.Valor.Value == 0)
            {
                throw ValidationExceptionFactory.Create("Itens", "Os itens aprovados da fatura precisam ter valor diferente de zero.");
            }

            var cartao = ResolverCartaoDaCompra(payload, contexto);
            var descricao = string.IsNullOrWhiteSpace(item.DescricaoAjustada)
                ? payload.Descricao ?? "Compra em cartÃƒÆ’Ã‚Â£o importada"
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
                        observacao: $"Gerada automaticamente a partir da importaÃƒÆ’Ã‚Â§ÃƒÆ’Ã‚Â£o {importacaoId}.",
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
                ?? throw ValidationExceptionFactory.Create("Itens", $"O item '{descricao}' nÃƒÆ’Ã‚Â£o possui data identificada.");
            chavesAfetadas.Add(new FaturaKey(cartao.Id, competenciaImportacaoAtual.Competencia));

            // Conciliação com lançamentos manuais: se a compra já foi registrada à mão
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
                observacao: $"Gerada automaticamente a partir da importaÃƒÆ’Ã‚Â§ÃƒÆ’Ã‚Â£o {importacaoId}.",
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

    /// <summary>
    /// Procura uma compra de cartão lançada manualmente que corresponda ao item importado:
    /// mesmo cartão, mesmo valor e data de compra com tolerância de ±3 dias.
    /// </summary>
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
            "NÃƒÆ’Ã‚Â£o foi possÃƒÆ’Ã‚Â­vel inferir a competÃƒÆ’Ã‚Âªncia atual da fatura importada para projetar as parcelas.");
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
                    observacao: $"Gerada automaticamente a partir da importaÃƒÂ§ÃƒÂ£o {importacaoId}.",
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

    private async Task RemoverGeracaoFinanceiraDaImportacaoAsync(Guid importacaoId, CancellationToken cancellationToken)
    {
        var contasGeradas = await dbContext.ContasPagar
            .Where(x => x.OrigemImportacaoWhatsappId == importacaoId)
            .ToListAsync(cancellationToken);

        if (contasGeradas.Count == 0)
        {
            return;
        }

        var contaIds = contasGeradas.Select(x => x.Id).ToArray();
        var movimentos = await dbContext.MovimentacoesFinanceiras
            .Where(x => x.ContaPagarId.HasValue)
            .WhereIn(x => x.ContaPagarId!.Value, contaIds)
            .ToListAsync(cancellationToken);
        var rateios = await dbContext.RateiosContaGerencial
            .Where(x => x.ContaPagarId.HasValue)
            .WhereIn(x => x.ContaPagarId!.Value, contaIds)
            .ToListAsync(cancellationToken);

        dbContext.MovimentacoesFinanceiras.RemoveRange(movimentos);
        dbContext.RateiosContaGerencial.RemoveRange(rateios);
        dbContext.ContasPagar.RemoveRange(contasGeradas);
    }

    private Task<bool> ImportacaoJaPossuiGeracaoFinanceiraAsync(Guid importacaoId, CancellationToken cancellationToken)
    {
        return dbContext.ContasPagar.AnyAsync(x => x.OrigemImportacaoWhatsappId == importacaoId, cancellationToken);
    }

    private async Task<AprovacaoFaturaContext> ValidarAprovacaoFaturaAsync(
        AprovarImportacaoWhatsappRequest? request,
        CancellationToken cancellationToken)
    {
        if (request?.RecebedorFaturaId is null || request.RecebedorFaturaId == Guid.Empty)
        {
            throw ValidationExceptionFactory.Create("RecebedorFaturaId", "Recebedor da fatura ÃƒÂ© obrigatÃƒÂ³rio.");
        }

        if (request.ResponsavelPagamentoFaturaId is null || request.ResponsavelPagamentoFaturaId == Guid.Empty)
        {
            throw ValidationExceptionFactory.Create("ResponsavelPagamentoFaturaId", "ResponsÃƒÂ¡vel pelo pagamento da fatura ÃƒÂ© obrigatÃƒÂ³rio.");
        }

        var cartaoIds = request.CartaoIds?
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray() ?? [];

        if (cartaoIds.Length == 0)
        {
            throw ValidationExceptionFactory.Create("CartaoIds", "Informe ao menos um cartÃƒÂ£o vinculado ÃƒÂ  fatura.");
        }

        if (!await dbContext.Pessoas.AnyAsync(x => x.Id == request.RecebedorFaturaId.Value, cancellationToken))
        {
            throw ValidationExceptionFactory.Create("RecebedorFaturaId", "Recebedor da fatura nÃƒÂ£o encontrado.");
        }

        if (!await dbContext.Pessoas.AnyAsync(x => x.Id == request.ResponsavelPagamentoFaturaId.Value, cancellationToken))
        {
            throw ValidationExceptionFactory.Create("ResponsavelPagamentoFaturaId", "ResponsÃƒÂ¡vel pelo pagamento nÃƒÂ£o encontrado.");
        }

        var cartoes = await dbContext.Cartoes
            .AsNoTracking()
            .Where(x => cartaoIds.Contains(x.Id) && x.Ativo)
            .ToArrayAsync(cancellationToken);

        if (cartoes.Length != cartaoIds.Length)
        {
            throw ValidationExceptionFactory.Create("CartaoIds", "Um ou mais cartÃƒÂµes vinculados nÃƒÂ£o foram encontrados.");
        }

        var formaPagamentoCartao = await dbContext.FormasPagamento
            .AsNoTracking()
            .Where(x => x.Ativo && x.EhCartao)
            .OrderBy(x => x.Nome)
            .FirstOrDefaultAsync(cancellationToken);

        if (formaPagamentoCartao is null)
        {
            throw ValidationExceptionFactory.Create("CartaoIds", "Cadastre uma forma de pagamento ativa do tipo cartÃƒÂ£o.");
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
                throw ValidationExceptionFactory.Create("CartaoIds", $"Mais de um cartÃƒÂ£o selecionado termina com {payload.CartaoFinal}.");
            }
        }

        if (contexto.Cartoes.Count == 1)
        {
            return contexto.Cartoes.Values.Single();
        }

        throw ValidationExceptionFactory.Create(
            "CartaoIds",
            "NÃƒÂ£o foi possÃƒÂ­vel identificar automaticamente o cartÃƒÂ£o do item importado. Ajuste os cartÃƒÂµes vinculados da fatura.");
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

    private async Task ProcessarImportacaoAsync(ImportacaoWhatsapp importacao, CancellationToken cancellationToken)
    {
        importacao.MarcarEmProcessamento();
        try
        {
            var extractionResult = await documentExtractor.ExtractAsync(
                new DocumentExtractionRequest(
                    importacao.TextoBruto,
                    importacao.NomeArquivo,
                    importacao.MimeType,
                    importacao.CaminhoArquivo),
                cancellationToken);

            if (!extractionResult.Success || string.IsNullOrWhiteSpace(extractionResult.TextoExtraido))
            {
                importacao.RegistrarErroExtracao(extractionResult.MensagemErro ?? "NÃ£o foi possÃ­vel extrair conteÃºdo da importaÃ§Ã£o.");
                return;
            }

            importacao.RegistrarExtracaoComSucesso(extractionResult.Confianca);

            var itens = await importSuggestionService.GenerateAsync(
                new ImportSuggestionRequest(
                    importacao.TipoOrigem,
                    importacao.Remetente,
                    extractionResult.TextoExtraido,
                    importacao.NomeArquivo,
                    importacao.MimeType),
                cancellationToken);

            var itensGerados = itens
                .Select(item =>
                {
                    var payload = ImportacaoWhatsappSuggestionPayload.Parse(item.PayloadSugeridoJson);
                    return ItemImportadoWhatsapp.Criar(
                        importacao.Id,
                        item.TipoSugestao,
                        item.PayloadSugeridoJson,
                        payload.BuildLearningKey());
                })
                .ToArray();

            importacao.SubstituirItens(itensGerados);
            dbContext.ItensImportadosWhatsapp.AddRange(itensGerados);
        }
        catch (Exception)
        {
            importacao.RegistrarErroExtracao("Falha ao integrar com o extrator ou a heuristica da importacao.");
        }
    }

    private static void ValidarWebhook(ReceberImportacaoWhatsappWebhookRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Remetente))
        {
            throw ValidationExceptionFactory.Create("Remetente", "Remetente Ã© obrigatÃ³rio.");
        }

        if (string.IsNullOrWhiteSpace(request.TextoBruto) && string.IsNullOrWhiteSpace(request.ArquivoBase64))
        {
            throw ValidationExceptionFactory.Create("TextoBruto", "Informe texto bruto ou arquivo para processar a importacao.");
        }

        if (!string.IsNullOrWhiteSpace(request.ArquivoBase64))
        {
            if (string.IsNullOrWhiteSpace(request.NomeArquivo))
            {
                throw ValidationExceptionFactory.Create("NomeArquivo", "Nome do arquivo Ã© obrigatÃ³rio quando houver artefato.");
            }

            if (string.IsNullOrWhiteSpace(request.MimeType))
            {
                throw ValidationExceptionFactory.Create("MimeType", "Mime type Ã© obrigatÃ³rio quando houver artefato.");
            }

            if (!MimeTypesSuportados.Contains(request.MimeType.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                throw ValidationExceptionFactory.Create("MimeType", "Mime type nÃ£o suportado para importaÃ§Ã£o.");
            }
        }
    }

    private async Task<ImportacaoWhatsapp?> CarregarImportacaoAsync(Guid id, CancellationToken cancellationToken)
    {
        return await dbContext.ImportacoesWhatsapp
            .Include(x => x.Itens)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    private async Task<ImportacaoWhatsapp?> CarregarImportacaoPorItemAsync(Guid itemId, CancellationToken cancellationToken)
    {
        var importacaoId = await dbContext.ItensImportadosWhatsapp
            .Where(x => x.Id == itemId)
            .Select(x => x.ImportacaoWhatsappId)
            .SingleOrDefaultAsync(cancellationToken);

        return importacaoId == Guid.Empty
            ? null
            : await CarregarImportacaoAsync(importacaoId, cancellationToken);
    }

    private async Task<ConfirmacaoClassificada> ValidarEClassificarConfirmacaoAsync(
        ItemImportadoWhatsapp item,
        RevisarItemImportadoWhatsappRequest request,
        CancellationToken cancellationToken)
    {
        var payload = ImportacaoWhatsappSuggestionPayload.Parse(item.PayloadSugeridoJson);
        var descricaoAjustada = SanitizarDescricaoAjustada(request.DescricaoAjustada, payload.Descricao);

        if (item.TipoSugestao == TipoSugestaoImportacaoWhatsapp.CompraCartao)
        {
            if (!request.ContaGerencialId.HasValue)
            {
                throw ValidationExceptionFactory.Create(
                    "ContaGerencialId",
                    "Conta gerencial Ã© obrigatÃ³ria para aprovar compras de cartÃ£o importadas.");
            }

            if (!request.ResponsavelId.HasValue)
            {
                throw ValidationExceptionFactory.Create(
                    "ResponsavelId",
                    "ResponsÃ¡vel Ã© obrigatÃ³rio para aprovar compras de cartÃ£o importadas.");
            }
        }

        if (item.TipoSugestao != TipoSugestaoImportacaoWhatsapp.CompraCartao)
        {
            if (!string.IsNullOrWhiteSpace(descricaoAjustada))
            {
                throw ValidationExceptionFactory.Create(
                    "DescricaoAjustada",
                    "A renomeaÃ§Ã£o amigÃ¡vel estÃ¡ disponÃ­vel apenas para compras de cartÃ£o importadas.");
            }

            if (request.MarcarComoRecorrente)
            {
                throw ValidationExceptionFactory.Create(
                    "MarcarComoRecorrente",
                    "A recorrÃªncia por histÃ³rico estÃ¡ disponÃ­vel apenas para compras de cartÃ£o importadas.");
            }
        }

        if (request.ResponsavelId.HasValue &&
            !await dbContext.Pessoas.AnyAsync(x => x.Id == request.ResponsavelId.Value, cancellationToken))
        {
            throw ValidationExceptionFactory.Create("ResponsavelId", "ResponsÃ¡vel nÃ£o encontrado.");
        }

        if (request.ContaGerencialId.HasValue)
        {
            var tipoEsperado = item.TipoSugestao switch
            {
                TipoSugestaoImportacaoWhatsapp.ContaReceber => TipoContaGerencial.Receita,
                TipoSugestaoImportacaoWhatsapp.ContaPagar => TipoContaGerencial.Despesa,
                TipoSugestaoImportacaoWhatsapp.CompraCartao => TipoContaGerencial.Despesa,
                _ => (TipoContaGerencial?)null
            };

            if (tipoEsperado.HasValue)
            {
                await ContaGerencialLancamentoValidator.ValidarContaLancavelPorTipoAsync(
                    dbContext,
                    request.ContaGerencialId.Value,
                    tipoEsperado.Value,
                    "ContaGerencialId",
                    "Conta gerencial nÃ£o encontrada.",
                    "Somente contas gerenciais filhas podem ser utilizadas na categorizacao.",
                    tipoEsperado.Value == TipoContaGerencial.Receita
                        ? "A categorizacao informada exige uma conta gerencial de receita."
                        : "A categorizacao informada exige uma conta gerencial de despesa.",
                    cancellationToken);
            }
            else
            {
                await ContaGerencialLancamentoValidator.ValidarContaLancavelAsync(
                    dbContext,
                    request.ContaGerencialId.Value,
                    "ContaGerencialId",
                    "Conta gerencial nÃ£o encontrada.",
                    "Somente contas gerenciais filhas podem ser utilizadas na categorizacao.",
                    cancellationToken);
            }
        }

        if (!request.GerarContaReceber)
        {
            return new ConfirmacaoClassificada(null, descricaoAjustada, request.MarcarComoRecorrente);
        }

        if (item.TipoSugestao != TipoSugestaoImportacaoWhatsapp.CompraCartao)
        {
            throw ValidationExceptionFactory.Create(
                "GerarContaReceber",
                "A geraÃ§Ã£o automÃ¡tica de conta a receber estÃ¡ disponÃ­vel apenas para compras de cartÃ£o importadas.");
        }

        if (!request.ResponsavelId.HasValue)
        {
            throw ValidationExceptionFactory.Create(
                "ResponsavelId",
                "ResponsÃ¡vel Ã© obrigatÃ³rio para gerar conta a receber a partir da fatura.");
        }

        if (string.IsNullOrWhiteSpace(payload.Descricao))
        {
            throw ValidationExceptionFactory.Create("Payload", "NÃ£o foi possÃ­vel identificar a descriÃ§Ã£o do item importado.");
        }

        if (!payload.Valor.HasValue || payload.Valor.Value <= 0)
        {
            throw ValidationExceptionFactory.Create("Payload", "NÃ£o foi possÃ­vel identificar um valor positivo para gerar a conta a receber.");
        }

        var dataVencimentoContaReceber = await ResolverDataVencimentoContaReceberAsync(payload, request, cancellationToken);
        if (!dataVencimentoContaReceber.HasValue)
        {
            throw ValidationExceptionFactory.Create(
                "DataVencimentoContaReceber",
                "Informe o vencimento do receber quando o documento nÃ£o trouxer o vencimento da fatura.");
        }

        var contaGerencialPadrao = await dbContext.ContasGerenciais
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.EhPadraoRecebimentoFaturaCartao, cancellationToken);

        if (contaGerencialPadrao is null)
        {
            throw ValidationExceptionFactory.Create(
                "GerarContaReceber",
                "Nenhuma conta gerencial padrÃ£o para recebimento de fatura foi configurada.");
        }

        await ContaGerencialLancamentoValidator.ValidarContaLancavelPorTipoAsync(
            dbContext,
            contaGerencialPadrao.Id,
            TipoContaGerencial.Receita,
            "GerarContaReceber",
            "Conta gerencial padrÃ£o para recebimento de fatura nÃ£o encontrada.",
            "A conta gerencial padrÃ£o para recebimento de fatura precisa ser lanÃ§Ã¡vel.",
            "A conta gerencial padrÃ£o para recebimento de fatura precisa ser do tipo receita.",
            cancellationToken);

        var formaPagamento = await dbContext.FormasPagamento
            .AsNoTracking()
            .Where(x => x.Ativo && !x.EhCartao)
            .OrderBy(x => x.Tipo == TipoFormaPagamento.Pix ? 0 : x.Tipo == TipoFormaPagamento.Transferencia ? 1 : 2)
            .ThenBy(x => x.Nome)
            .FirstOrDefaultAsync(cancellationToken);

        if (formaPagamento is null)
        {
            throw ValidationExceptionFactory.Create(
                "GerarContaReceber",
                "Cadastre ao menos uma forma de pagamento ativa nÃ£o-cartÃ£o para gerar a conta a receber.");
        }

        var contaReceber = ContaReceber.Criar(
            numeroDocumento: null,
            dataEmissao: payload.DataIdentificada ?? DateOnly.FromDateTime(DateTime.UtcNow),
            responsavelId: request.ResponsavelId,
            pagadorId: request.ResponsavelId.Value,
            dataVencimento: dataVencimentoContaReceber.Value,
            formaPagamentoId: formaPagamento.Id,
            cartaoId: null,
            contaBancariaId: null,
            valorOriginal: payload.Valor.Value,
            valorDesconto: 0m,
            valorJuros: 0m,
            valorMulta: 0m,
            quantidadeParcelas: 1,
            numeroParcela: 1,
            grupoParcelamentoId: null,
            descricao: descricaoAjustada ?? payload.Descricao,
            observacao: "Gerada automaticamente a partir da revisÃ£o de item importado da fatura.",
            statusContaId: StatusConta.PendenteId,
            ehRecorrente: false,
            regraRecorrenciaId: null,
            origem: OrigemLancamento.Importacao,
            rateios:
            [
                RateioPlano.Create(contaGerencialPadrao.Id, payload.Valor.Value)
            ]);

        dbContext.ContasReceber.Add(contaReceber);
        dbContext.RateiosContaGerencial.AddRange(contaReceber.Rateios);

        return new ConfirmacaoClassificada(contaReceber.Id, descricaoAjustada, request.MarcarComoRecorrente);
    }

    private async Task<DateOnly?> ResolverDataVencimentoContaReceberAsync(
        ImportacaoWhatsappSuggestionPayload payload,
        RevisarItemImportadoWhatsappRequest request,
        CancellationToken cancellationToken)
    {
        if (payload.DataVencimento.HasValue)
        {
            return payload.DataVencimento.Value;
        }

        if (request.DataVencimentoContaReceber.HasValue)
        {
            return request.DataVencimentoContaReceber.Value;
        }

        if (string.IsNullOrWhiteSpace(payload.CartaoFinal))
        {
            return null;
        }

        var cartoes = await dbContext.Cartoes
            .AsNoTracking()
            .Where(x => x.Ativo && x.NumeroFinal == payload.CartaoFinal)
            .ToArrayAsync(cancellationToken);

        if (cartoes.Length != 1)
        {
            return null;
        }

        var dataCompra = payload.DataIdentificada ?? DateOnly.FromDateTime(DateTime.UtcNow);
        return FaturaCartaoCompetencia.Calcular(
            dataCompra,
            cartoes[0].DiaFechamentoFatura,
            cartoes[0].DiaVencimentoFatura).DataVencimento;
    }

    private static string? SanitizarDescricaoAjustada(string? descricaoAjustada, string? descricaoOriginal)
    {
        if (string.IsNullOrWhiteSpace(descricaoAjustada))
        {
            return null;
        }

        var ajustada = descricaoAjustada.Trim();
        if (string.IsNullOrWhiteSpace(ajustada))
        {
            return null;
        }

        return string.Equals(ajustada, descricaoOriginal?.Trim(), StringComparison.OrdinalIgnoreCase)
            ? null
            : ajustada;
    }

    private static void ValidarImportacaoNaoAprovada(ImportacaoWhatsapp importacao)
    {
        if (importacao.Status == StatusImportacaoWhatsapp.Confirmado)
        {
            throw new InvalidOperationException("ImportaÃ§Ã£o aprovada nÃ£o pode ser alterada. Reabra a importaÃ§Ã£o para editar os itens.");
        }
    }

    private static TipoOrigemImportacaoWhatsapp MapearTipoOrigem(TipoOrigemImportacaoWhatsappRequest tipoOrigem)
    {
        return tipoOrigem switch
        {
            TipoOrigemImportacaoWhatsappRequest.Texto => TipoOrigemImportacaoWhatsapp.Texto,
            TipoOrigemImportacaoWhatsappRequest.Imagem => TipoOrigemImportacaoWhatsapp.Imagem,
            TipoOrigemImportacaoWhatsappRequest.Pdf => TipoOrigemImportacaoWhatsapp.Pdf,
            TipoOrigemImportacaoWhatsappRequest.Arquivo => TipoOrigemImportacaoWhatsapp.Arquivo,
            _ => throw new ApplicationValidationException("Um ou mais campos sÃ£o invÃ¡lidos.", new Dictionary<string, string[]>
            {
                ["TipoOrigem"] = ["Tipo de origem invÃ¡lido."]
            })
        };
    }

    private sealed record ConfirmacaoClassificada(
        Guid? ContaReceberId,
        string? DescricaoAjustada,
        bool MarcarComoRecorrente);

    private sealed record AprovacaoFaturaContext(
        Guid RecebedorFaturaId,
        Guid ResponsavelPagamentoFaturaId,
        Guid FormaPagamentoCartaoId,
        IReadOnlyDictionary<Guid, Domain.Cadastros.Cartoes.Cartao> Cartoes);

    private sealed record FaturaKey(Guid CartaoId, string Competencia);

    private sealed record ImportacaoMaterializadaResult(
        IReadOnlyCollection<ContaPagar> ContasGeradas,
        IReadOnlyCollection<FaturaKey> ChavesAfetadas,
        IReadOnlyCollection<FaturaKey> ChavesContaPagarFatura);

    private sealed record ItemImportadoPreparado(
        ItemImportadoWhatsapp Item,
        ImportacaoWhatsappSuggestionPayload Payload,
        Cartao Cartao,
        string Descricao,
        Guid ContaGerencialId,
        Guid ResponsavelId,
        decimal Valor);
}
