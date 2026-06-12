using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;
using ControleFinanceiro.Domain.Financeiro;
using ControleFinanceiro.Domain.ImportacoesWhatsapp;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace ControleFinanceiro.Application.ImportacoesWhatsapp;

public interface IHeuristicPredictionService
{
    Task<Dictionary<Guid, HistoricalPredictionData>> CalcularPredicoesAsync(
        IReadOnlyCollection<ItemImportadoWhatsapp> itens,
        CancellationToken cancellationToken);

    Task<Dictionary<Guid, CardPurchaseForecastStatusData>> CalcularStatusPrevisaoCompraCartaoAsync(
        IReadOnlyCollection<ItemImportadoWhatsapp> itens,
        CancellationToken cancellationToken);
}

public sealed class HeuristicPredictionService(
    IAppDbContext dbContext,
    IHeuristicMatchingService heuristicMatchingService) : IHeuristicPredictionService
{
    public async Task<Dictionary<Guid, HistoricalPredictionData>> CalcularPredicoesAsync(
        IReadOnlyCollection<ItemImportadoWhatsapp> itens,
        CancellationToken cancellationToken)
    {
        var chaves = itens
            .Select(item => item.ChaveAprendizado ?? ImportacaoWhatsappSuggestionPayload.Parse(item.PayloadSugeridoJson).BuildLearningKey())
            .Where(chave => !string.IsNullOrWhiteSpace(chave))
            .Distinct()
            .ToArray();

        if (chaves.Length == 0)
        {
            return [];
        }

        var itemIds = itens.Select(x => x.Id).ToArray();
        var importacoesAprovadas = dbContext.ImportacoesWhatsapp
            .AsNoTracking()
            .Where(x => x.Status == StatusImportacaoWhatsapp.Confirmado)
            .Select(x => x.Id);

        var historico = await dbContext.ItensImportadosWhatsapp
            .AsNoTracking()
            .Where(x =>
                x.Status == StatusItemImportadoWhatsapp.Confirmado &&
                importacoesAprovadas.Contains(x.ImportacaoWhatsappId) &&
                x.ChaveAprendizado != null &&
                chaves.Contains(x.ChaveAprendizado) &&
                !itemIds.Contains(x.Id))
            .Select(x => new HistoricalPredictionCandidate(
                x.TipoSugestao,
                x.ChaveAprendizado!,
                x.ContaGerencialId,
                x.ResponsavelId,
                x.DescricaoAjustada,
                x.ContaReceberId,
                x.MarcarComoRecorrente))
            .ToArrayAsync(cancellationToken);

        var contasGerenciaisHeuristicas = await CarregarContasGerenciaisHeuristicasAsync(itens, cancellationToken);
        var predicoes = new Dictionary<Guid, HistoricalPredictionData>();

        foreach (var item in itens)
        {
            var payload = ImportacaoWhatsappSuggestionPayload.Parse(item.PayloadSugeridoJson);
            var chaveAprendizado = item.ChaveAprendizado ?? payload.BuildLearningKey();
            if (string.IsNullOrWhiteSpace(chaveAprendizado))
            {
                continue;
            }

            var candidatos = historico
                .Where(x =>
                    x.TipoSugestao == item.TipoSugestao &&
                    x.ChaveAprendizado == chaveAprendizado &&
                    (x.ContaGerencialId.HasValue ||
                     x.ResponsavelId.HasValue ||
                     x.ContaReceberId.HasValue ||
                     x.MarcarComoRecorrente ||
                     !string.IsNullOrWhiteSpace(x.DescricaoAjustada)))
                .ToArray();

            if (candidatos.Length == 0)
            {
                var predicaoHeuristica = item.TipoSugestao == TipoSugestaoImportacaoWhatsapp.CompraCartao
                    ? heuristicMatchingService.TentarPredicaoHeuristicaCompraCartao(payload, contasGerenciaisHeuristicas)
                    : null;

                if (predicaoHeuristica is not null)
                {
                    predicoes[item.Id] = predicaoHeuristica;
                }

                continue;
            }

            var melhorAgrupamento = candidatos
                .GroupBy(x => new
                {
                    x.ContaGerencialId,
                    x.ResponsavelId,
                    x.DescricaoAjustada,
                    GerarContaReceber = x.ContaReceberId.HasValue,
                    x.MarcarComoRecorrente
                })
                .Select(x => new
                {
                    x.Key.ContaGerencialId,
                    x.Key.ResponsavelId,
                    x.Key.DescricaoAjustada,
                    x.Key.GerarContaReceber,
                    x.Key.MarcarComoRecorrente,
                    Quantidade = x.Count()
                })
                .OrderByDescending(x => x.Quantidade)
                .ThenByDescending(x => x.MarcarComoRecorrente)
                .ThenByDescending(x => x.GerarContaReceber)
                .First();

            predicoes[item.Id] = new HistoricalPredictionData(
                melhorAgrupamento.ContaGerencialId,
                melhorAgrupamento.ResponsavelId,
                melhorAgrupamento.DescricaoAjustada,
                melhorAgrupamento.GerarContaReceber,
                melhorAgrupamento.MarcarComoRecorrente,
                melhorAgrupamento.Quantidade,
                decimal.Round(
                    melhorAgrupamento.Quantidade / (decimal)candidatos.Length,
                    2,
                    MidpointRounding.AwayFromZero));
        }

        return predicoes;
    }

    public async Task<Dictionary<Guid, CardPurchaseForecastStatusData>> CalcularStatusPrevisaoCompraCartaoAsync(
        IReadOnlyCollection<ItemImportadoWhatsapp> itens,
        CancellationToken cancellationToken)
    {
        var comprasCartao = itens
            .Where(item => item.TipoSugestao == TipoSugestaoImportacaoWhatsapp.CompraCartao)
            .ToArray();

        if (comprasCartao.Length == 0)
        {
            return [];
        }

        var itemIds = comprasCartao.Select(x => x.Id).ToArray();
        var importacoesAprovadas = dbContext.ImportacoesWhatsapp
            .AsNoTracking()
            .Where(x => x.Status == StatusImportacaoWhatsapp.Confirmado)
            .Select(x => x.Id);
        var historicoBruto = await dbContext.ItensImportadosWhatsapp
            .AsNoTracking()
            .Where(x =>
                x.Status == StatusItemImportadoWhatsapp.Confirmado &&
                x.TipoSugestao == TipoSugestaoImportacaoWhatsapp.CompraCartao &&
                importacoesAprovadas.Contains(x.ImportacaoWhatsappId) &&
                !itemIds.Contains(x.Id))
            .Select(x => new HistoricalCardPurchaseCandidate(
                x.ChaveAprendizado,
                x.MarcarComoRecorrente,
                x.PayloadSugeridoJson))
            .ToArrayAsync(cancellationToken);

        var historico = historicoBruto
            .Select(item =>
            {
                var payload = ImportacaoWhatsappSuggestionPayload.Parse(item.PayloadSugeridoJson);
                return new HistoricalCardPurchaseData(
                    item.ChaveAprendizado ?? payload.BuildLearningKey(),
                    item.MarcarComoRecorrente,
                    payload.BuildInstallmentSeriesKey(),
                    payload.GetParcelamentoCompraCartaoInfo());
            })
            .ToArray();

        var statusPorItemId = new Dictionary<Guid, CardPurchaseForecastStatusData>(comprasCartao.Length);

        foreach (var item in comprasCartao)
        {
            var payload = ImportacaoWhatsappSuggestionPayload.Parse(item.PayloadSugeridoJson);
            var parcelamento = payload.GetParcelamentoCompraCartaoInfo();

            var previsto = parcelamento is not null && parcelamento.NumeroParcela > 1
                ? VerificarCompraParceladaPrevista(payload, parcelamento, historico)
                : VerificarCompraRecorrentePrevista(item, payload, historico);

            statusPorItemId[item.Id] = previsto
                ? new CardPurchaseForecastStatusData("PREVISTO", "Previsto")
                : new CardPurchaseForecastStatusData("NAO_PREVISTO", "Não previsto");
        }

        return statusPorItemId;
    }

    private async Task<ContaGerencialHeuristicaData[]> CarregarContasGerenciaisHeuristicasAsync(
        IReadOnlyCollection<ItemImportadoWhatsapp> itens,
        CancellationToken cancellationToken)
    {
        if (!itens.Any(item => item.TipoSugestao == TipoSugestaoImportacaoWhatsapp.CompraCartao))
        {
            return [];
        }

        var contas = await dbContext.ContasGerenciais
            .AsNoTracking()
            .Where(x => x.Ativo && x.Tipo == TipoContaGerencial.Despesa)
            .Select(x => new ContaGerencialHeuristicaData(
                x.Id,
                x.Codigo,
                x.Descricao,
                x.ResponsavelPadraoId,
                x.ContaPaiId))
            .ToArrayAsync(cancellationToken);

        var idsEstruturais = contas
            .Where(conta => conta.ContaPaiId.HasValue)
            .Select(conta => conta.ContaPaiId!.Value)
            .ToHashSet();

        return contas
            .Where(conta => !idsEstruturais.Contains(conta.Id))
            .ToArray();
    }

    private static bool VerificarCompraParceladaPrevista(
        ImportacaoWhatsappSuggestionPayload payload,
        ParcelamentoCompraCartaoInfo parcelamentoAtual,
        IReadOnlyCollection<HistoricalCardPurchaseData> historico)
    {
        var installmentSeriesKey = payload.BuildInstallmentSeriesKey();
        if (string.IsNullOrWhiteSpace(installmentSeriesKey))
        {
            return false;
        }

        return historico.Any(item =>
            item.InstallmentSeriesKey == installmentSeriesKey &&
            item.Parcelamento is not null &&
            item.Parcelamento.QuantidadeParcelas == parcelamentoAtual.QuantidadeParcelas &&
            item.Parcelamento.NumeroParcela < parcelamentoAtual.NumeroParcela);
    }

    private static bool VerificarCompraRecorrentePrevista(
        ItemImportadoWhatsapp item,
        ImportacaoWhatsappSuggestionPayload payload,
        IReadOnlyCollection<HistoricalCardPurchaseData> historico)
    {
        var chaveAprendizado = item.ChaveAprendizado ?? payload.BuildLearningKey();
        if (string.IsNullOrWhiteSpace(chaveAprendizado))
        {
            return false;
        }

        return historico.Any(entry =>
            entry.ChaveAprendizado == chaveAprendizado &&
            entry.MarcarComoRecorrente);
    }

    private sealed record HistoricalPredictionCandidate(
        TipoSugestaoImportacaoWhatsapp TipoSugestao,
        string ChaveAprendizado,
        Guid? ContaGerencialId,
        Guid? ResponsavelId,
        string? DescricaoAjustada,
        Guid? ContaReceberId,
        bool MarcarComoRecorrente);

    private sealed record HistoricalCardPurchaseCandidate(
        string? ChaveAprendizado,
        bool MarcarComoRecorrente,
        string PayloadSugeridoJson);

    private sealed record HistoricalCardPurchaseData(
        string? ChaveAprendizado,
        bool MarcarComoRecorrente,
        string? InstallmentSeriesKey,
        ParcelamentoCompraCartaoInfo? Parcelamento);
}