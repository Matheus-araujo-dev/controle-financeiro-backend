using System.Text.Json;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.ImportacoesWhatsapp;
using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;
using ControleFinanceiro.Domain.Financeiro;
using ControleFinanceiro.Domain.ImportacoesWhatsapp;
using ControleFinanceiro.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ControleFinanceiro.Application.Dashboard;

/// <summary>Shared DB query helpers injected into all focused dashboard services.</summary>
public sealed class DashboardDbHelpers(
    IAppDbContext dbContext,
    ICurrentUser currentUser,
    ILogger<DashboardDbHelpers> logger)
{
    internal async Task<decimal> CalcularSaldoRealizadoAteAsync(DateOnly dataLimite, CancellationToken cancellationToken)
    {
        var saldoInicialContas = await dbContext.ContasBancarias
            .AsNoTracking()
            .Where(c => c.Ativo)
            .SumAsync(c => (decimal?)c.SaldoInicial, cancellationToken) ?? 0m;

        var movimentos = await dbContext.MovimentacoesFinanceiras
            .AsNoTracking()
            .Where(m =>
                m.Natureza == NaturezaMovimentacao.Realizada &&
                m.StatusMovimentacaoId != StatusMovimentacao.CanceladaId &&
                m.DataMovimentacao <= dataLimite)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalEntradas = g.Sum(m => m.Tipo == TipoMovimentacao.Entrada ? m.Valor : 0),
                TotalSaidas = g.Sum(m => m.Tipo == TipoMovimentacao.Saida ? m.Valor : 0)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return decimal.Round(
            saldoInicialContas + (movimentos?.TotalEntradas ?? 0) - (movimentos?.TotalSaidas ?? 0),
            2, MidpointRounding.AwayFromZero);
    }

    internal async Task<IReadOnlyDictionary<Guid, string>> CarregarPessoasAsync(CancellationToken cancellationToken) =>
        await dbContext.Pessoas.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Nome, cancellationToken);

    internal async Task<IReadOnlyDictionary<Guid, ContaGerencialInfo>> CarregarContasGerenciaisAsync(CancellationToken cancellationToken) =>
        await dbContext.ContasGerenciais
            .AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => new ContaGerencialInfo(c.Codigo, c.Descricao, c.Tipo), cancellationToken);

    internal async Task<(Dictionary<Guid, Guid?> PorContaPagar, Dictionary<Guid, Guid?> PorContaReceber)> CarregarRateioPrincipalAsync(
        IReadOnlyCollection<Guid> contasPagarIds,
        IReadOnlyCollection<Guid> contasReceberIds,
        CancellationToken cancellationToken)
    {
        if (contasPagarIds.Count == 0 && contasReceberIds.Count == 0)
            return ([], []);

        var rateios = await dbContext.RateiosContaGerencial
            .AsNoTracking()
            .Where(r =>
                (r.ContaPagarId != null && contasPagarIds.Contains(r.ContaPagarId.Value)) ||
                (r.ContaReceberId != null && contasReceberIds.Contains(r.ContaReceberId.Value)))
            .Select(r => new { r.ContaPagarId, r.ContaReceberId, r.ContaGerencialId, r.Valor })
            .ToListAsync(cancellationToken);

        var porContaPagar = rateios
            .Where(r => r.ContaPagarId.HasValue)
            .GroupBy(r => r.ContaPagarId!.Value)
            .ToDictionary(g => g.Key, g => (Guid?)g.OrderByDescending(r => r.Valor).First().ContaGerencialId);

        var porContaReceber = rateios
            .Where(r => r.ContaReceberId.HasValue)
            .GroupBy(r => r.ContaReceberId!.Value)
            .ToDictionary(g => g.Key, g => (Guid?)g.OrderByDescending(r => r.Valor).First().ContaGerencialId);

        return (porContaPagar, porContaReceber);
    }

    internal async Task<List<RateioLancamentoInfo>> CarregarRateiosPorEmissaoAsync(
        DateOnly dataInicial,
        DateOnly dataFinal,
        CancellationToken cancellationToken)
    {
        var rateiosPagar = await (
            from rateio in dbContext.RateiosContaGerencial.AsNoTracking()
            join conta in dbContext.ContasPagar.AsNoTracking() on rateio.ContaPagarId equals (Guid?)conta.Id
            where conta.DataEmissao >= dataInicial &&
                  conta.DataEmissao <= dataFinal &&
                  conta.StatusContaId != StatusConta.CanceladaId
            select new RateioLancamentoInfo(
                conta.Id, "ContaPagar", conta.Descricao, conta.RecebedorId,
                conta.DataEmissao, conta.DataVencimento, conta.ValorLiquido,
                rateio.Valor, conta.StatusContaId, rateio.ContaGerencialId))
            .ToListAsync(cancellationToken);

        var rateiosReceber = await (
            from rateio in dbContext.RateiosContaGerencial.AsNoTracking()
            join conta in dbContext.ContasReceber.AsNoTracking() on rateio.ContaReceberId equals (Guid?)conta.Id
            where conta.DataEmissao >= dataInicial &&
                  conta.DataEmissao <= dataFinal &&
                  conta.StatusContaId != StatusConta.CanceladaId
            select new RateioLancamentoInfo(
                conta.Id, "ContaReceber", conta.Descricao, conta.PagadorId,
                conta.DataEmissao, conta.DataVencimento, conta.ValorLiquido,
                rateio.Valor, conta.StatusContaId, rateio.ContaGerencialId))
            .ToListAsync(cancellationToken);

        rateiosPagar.AddRange(rateiosReceber);
        return rateiosPagar;
    }

    internal async Task<List<ImportacaoCompraInfo>> CarregarComprasImportadasAsync(CancellationToken cancellationToken)
    {
        var familiaId = currentUser.FamiliaId;

        var registros = await dbContext.ItensImportadosWhatsapp
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(i =>
                i.TipoSugestao == TipoSugestaoImportacaoWhatsapp.CompraCartao &&
                i.Status == StatusItemImportadoWhatsapp.Confirmado &&
                (familiaId == null || i.FamiliaId == familiaId.Value || i.FamiliaId == Guid.Empty))
            .ToListAsync(cancellationToken);

        var compras = new List<ImportacaoCompraInfo>(registros.Count);

        foreach (var item in registros)
        {
            ImportacaoWhatsappSuggestionPayload payload;
            try
            {
                payload = ImportacaoWhatsappSuggestionPayload.Parse(item.PayloadSugeridoJson);
            }
            catch (JsonException exception)
            {
                logger.LogWarning(exception, "Payload inválido no item importado {ItemId}; item ignorado no dashboard.", item.Id);
                continue;
            }

            var dataVencimento = payload.DataVencimento ?? payload.DataIdentificada;
            var dataCompra = payload.DataIdentificada ?? payload.DataVencimento;

            if (!dataVencimento.HasValue || !dataCompra.HasValue || !payload.Valor.HasValue)
                continue;

            var tipo = string.Equals(payload.TipoMovimentacaoSugerido, "Entrada", StringComparison.OrdinalIgnoreCase)
                ? TipoMovimentacao.Entrada
                : TipoMovimentacao.Saida;

            compras.Add(new ImportacaoCompraInfo(
                item.Id,
                item.DescricaoAjustada ?? payload.Descricao ?? "Compra importada",
                payload.Valor.Value,
                dataCompra.Value,
                dataVencimento.Value,
                tipo,
                item.MarcarComoRecorrente,
                payload.GetParcelamentoCompraCartaoInfo(),
                payload.BuildRecurringSeriesKey(),
                payload.BuildInstallmentSeriesKey(),
                item.ContaGerencialId,
                item.ResponsavelId));
        }

        return compras;
    }

    internal async Task<List<ContaJanelaInfo>> CarregarContasJanelaAsync(
        string tipo, DateOnly dataInicial, DateOnly dataFinal, CancellationToken cancellationToken)
    {
        if (tipo == "ContaPagar")
        {
            return await dbContext.ContasPagar
                .AsNoTracking()
                .Where(c => c.StatusContaId != StatusConta.CanceladaId &&
                            c.DataVencimento >= dataInicial && c.DataVencimento <= dataFinal)
                .Select(c => new ContaJanelaInfo(c.Id, "ContaPagar", c.Descricao, c.DataVencimento,
                    c.ValorLiquido, c.StatusContaId, c.RecebedorId, c.ResponsavelCompraId,
                    c.RegraRecorrenciaId, c.Origem))
                .ToListAsync(cancellationToken);
        }

        return await dbContext.ContasReceber
            .AsNoTracking()
            .Where(c => c.StatusContaId != StatusConta.CanceladaId &&
                        c.DataVencimento >= dataInicial && c.DataVencimento <= dataFinal)
            .Select(c => new ContaJanelaInfo(c.Id, "ContaReceber", c.Descricao, c.DataVencimento,
                c.ValorLiquido, c.StatusContaId, c.PagadorId, c.ResponsavelId,
                c.RegraRecorrenciaId, c.Origem))
            .ToListAsync(cancellationToken);
    }

    internal async Task<List<RecorrenciaProjetada>> ProjetarRecorrenciasAsync(
        DateOnly dataInicial,
        DateOnly dataFinal,
        CancellationToken cancellationToken)
    {
        var regras = await dbContext.RegrasRecorrencia
            .AsNoTracking()
            .Where(r => r.Ativa)
            .ToListAsync(cancellationToken);

        if (regras.Count == 0) return [];

        var regraIds = regras.Select(r => r.Id).ToArray();

        var contasPagarRegra = await dbContext.ContasPagar
            .AsNoTracking()
            .Where(c => c.RegraRecorrenciaId != null && regraIds.Contains(c.RegraRecorrenciaId.Value) && c.StatusContaId != StatusConta.CanceladaId)
            .Select(c => new ContaRecorrenciaInfo(c.Id, c.RegraRecorrenciaId!.Value, c.DataVencimento, c.ValorLiquido, c.Descricao, c.Origem, c.RecebedorId, c.ResponsavelCompraId))
            .ToListAsync(cancellationToken);

        var contasReceberRegra = await dbContext.ContasReceber
            .AsNoTracking()
            .Where(c => c.RegraRecorrenciaId != null && regraIds.Contains(c.RegraRecorrenciaId.Value) && c.StatusContaId != StatusConta.CanceladaId)
            .Select(c => new ContaRecorrenciaInfo(c.Id, c.RegraRecorrenciaId!.Value, c.DataVencimento, c.ValorLiquido, c.Descricao, c.Origem, c.PagadorId, c.ResponsavelId))
            .ToListAsync(cancellationToken);

        var projecoes = new List<RecorrenciaProjetada>();

        foreach (var regra in regras)
        {
            var ehContaPagar = regra.TipoLancamento == TipoLancamentoRecorrencia.ContaPagar;
            var contasDaRegra = (ehContaPagar ? contasPagarRegra : contasReceberRegra)
                .Where(c => c.RegraId == regra.Id)
                .OrderBy(c => c.DataVencimento)
                .ToList();

            if (contasDaRegra.Count == 0) continue;

            var template = contasDaRegra.FirstOrDefault(c => c.Origem != OrigemLancamento.Recorrencia) ?? contasDaRegra[^1];
            var mesesComOcorrencia = contasDaRegra
                .Select(c => new DateOnly(c.DataVencimento.Year, c.DataVencimento.Month, 1))
                .ToHashSet();

            foreach (var data in DashboardHelpers.CalcularDatasProjetadas(regra, mesesComOcorrencia, dataInicial, dataFinal))
            {
                projecoes.Add(new RecorrenciaProjetada(
                    regra.Id, data,
                    ehContaPagar ? TipoMovimentacao.Saida : TipoMovimentacao.Entrada,
                    template.ValorLiquido, template.Descricao,
                    template.PessoaId, template.ResponsavelId,
                    template.Id, ehContaPagar));
            }
        }

        return projecoes;
    }
}
