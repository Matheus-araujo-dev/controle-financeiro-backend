using ControleFinanceiro.Contracts.Dashboard;
using ControleFinanceiro.Domain.Financeiro;

namespace ControleFinanceiro.Application.Dashboard;

public interface IDashboardCentralPrevisaoService
{
    Task<DashboardCentralPrevisaoResumoResponse> ObterCentralPrevisaoResumoAsync(DashboardCentralPrevisaoQueryRequest query, CancellationToken cancellationToken);
    Task<DashboardCentralPrevisaoItensResponse> ObterCentralPrevisaoItensAsync(DashboardCentralPrevisaoItensQueryRequest query, CancellationToken cancellationToken);
}

public sealed class DashboardCentralPrevisaoService(DashboardDbHelpers db) : IDashboardCentralPrevisaoService
{
    public async Task<DashboardCentralPrevisaoResumoResponse> ObterCentralPrevisaoResumoAsync(
        DashboardCentralPrevisaoQueryRequest query, CancellationToken cancellationToken)
    {
        var (dataInicial, dias, mesReferenciaEhMesAtual) = DashboardHelpers.ResolverJanela(query.MesReferencia, query.DataInicial, query.Dias);
        var itens = await ConstruirItensPrevisaoAsync(dataInicial, dias, !mesReferenciaEhMesAtual, cancellationToken);

        var grupos = itens
            .Where(item => (!query.Origem.HasValue || item.Origem == query.Origem.Value) &&
                           (!query.Status.HasValue || item.Status == query.Status.Value))
            .GroupBy(item => new { item.Data, item.Tipo, item.Origem, item.Status })
            .OrderBy(g => g.Key.Data).ThenBy(g => g.Key.Origem).ThenBy(g => g.Key.Status)
            .Select(g => new DashboardCentralPrevisaoResumoItemResponse(
                g.Key.Data,
                DashboardHelpers.MapearTipoMovimentacao(g.Key.Tipo),
                g.Key.Origem, g.Key.Status,
                g.Count(),
                decimal.Round(g.Sum(item => item.Valor), 2, MidpointRounding.AwayFromZero)))
            .ToList();

        return new DashboardCentralPrevisaoResumoResponse(dataInicial, dias, query.Origem, query.Status, grupos);
    }

    public async Task<DashboardCentralPrevisaoItensResponse> ObterCentralPrevisaoItensAsync(
        DashboardCentralPrevisaoItensQueryRequest query, CancellationToken cancellationToken)
    {
        var (dataInicial, dias, mesReferenciaEhMesAtual) = DashboardHelpers.ResolverJanela(query.MesReferencia, query.DataInicial, query.Dias);
        var itensPrevisao = await ConstruirItensPrevisaoAsync(dataInicial, dias, !mesReferenciaEhMesAtual, cancellationToken);
        var contasGerenciais = await db.CarregarContasGerenciaisAsync(cancellationToken);

        var itens = itensPrevisao
            .Where(item => (!query.Data.HasValue || item.Data == query.Data.Value) &&
                           (!query.Origem.HasValue || item.Origem == query.Origem.Value) &&
                           (!query.Status.HasValue || item.Status == query.Status.Value))
            .OrderBy(item => item.Data).ThenBy(item => item.Descricao)
            .Select(item =>
            {
                var cg = item.ContaGerencialId.HasValue ? contasGerenciais.GetValueOrDefault(item.ContaGerencialId.Value) : null;
                return new DashboardCentralPrevisaoItemResponse(
                    item.TipoReferencia, item.ReferenciaId, item.Data,
                    DashboardHelpers.MapearTipoMovimentacao(item.Tipo),
                    item.Origem, item.Status, item.Descricao, item.Valor,
                    item.PessoaNome, item.ResponsavelNome,
                    item.ContaGerencialId, cg?.Codigo, cg?.Descricao);
            })
            .ToList();

        return new DashboardCentralPrevisaoItensResponse(dataInicial, dias, query.Data, query.Origem, query.Status, itens);
    }

    private async Task<List<PrevisaoItem>> ConstruirItensPrevisaoAsync(
        DateOnly dataInicial, int dias, bool incluirRecorrencias, CancellationToken cancellationToken)
    {
        var dataFinal = dataInicial.AddDays(dias - 1);
        var pessoas = await db.CarregarPessoasAsync(cancellationToken);
        var itens = new List<PrevisaoItem>();

        var contasPagar = await db.CarregarContasJanelaAsync("ContaPagar", dataInicial, dataFinal, cancellationToken);
        var contasReceber = await db.CarregarContasJanelaAsync("ContaReceber", dataInicial, dataFinal, cancellationToken);
        var recorrencias = incluirRecorrencias
            ? await db.ProjetarRecorrenciasAsync(dataInicial, dataFinal, cancellationToken)
            : [];

        var rateioPrincipal = await db.CarregarRateioPrincipalAsync(
            contasPagar.Where(c => c.TipoLancamento == "ContaPagar").Select(c => c.Id)
                .Concat(recorrencias.Where(r => r.EhContaPagar).Select(r => r.ContaTemplateId)).Distinct().ToArray(),
            contasReceber.Select(c => c.Id)
                .Concat(recorrencias.Where(r => !r.EhContaPagar).Select(r => r.ContaTemplateId)).Distinct().ToArray(),
            cancellationToken);

        foreach (var conta in contasPagar.Concat(contasReceber))
        {
            var origem = ClassificarOrigem(conta.Origem, conta.RegraRecorrenciaId);
            if (!incluirRecorrencias && origem is DashboardCentralPrevisaoOrigem.Recorrencia or DashboardCentralPrevisaoOrigem.ContaFuturaGerada)
                continue;

            var ehContaPagar = conta.TipoLancamento == "ContaPagar";
            var lookup = ehContaPagar ? rateioPrincipal.PorContaPagar : rateioPrincipal.PorContaReceber;

            itens.Add(new PrevisaoItem(
                conta.TipoLancamento, conta.Id, conta.DataVencimento,
                ehContaPagar ? TipoMovimentacao.Saida : TipoMovimentacao.Entrada,
                origem,
                conta.StatusContaId == StatusConta.LiquidadaId
                    ? DashboardCentralPrevisaoStatus.Realizado
                    : DashboardCentralPrevisaoStatus.Substituido,
                conta.Descricao, conta.ValorLiquido,
                pessoas.GetValueOrDefault(conta.PessoaId),
                conta.ResponsavelId.HasValue ? pessoas.GetValueOrDefault(conta.ResponsavelId.Value) : null,
                lookup.TryGetValue(conta.Id, out var cgId) ? cgId : null));
        }

        foreach (var rec in recorrencias)
        {
            var lookup = rec.EhContaPagar ? rateioPrincipal.PorContaPagar : rateioPrincipal.PorContaReceber;
            itens.Add(new PrevisaoItem(
                "RegraRecorrencia", rec.RegraId, rec.Data, rec.Tipo,
                DashboardCentralPrevisaoOrigem.Recorrencia,
                DashboardCentralPrevisaoStatus.Previsto,
                rec.Descricao, rec.Valor,
                pessoas.GetValueOrDefault(rec.PessoaId),
                rec.ResponsavelId.HasValue ? pessoas.GetValueOrDefault(rec.ResponsavelId.Value) : null,
                lookup.TryGetValue(rec.ContaTemplateId, out var cgId) ? cgId : null));
        }

        var comprasImportadas = await db.CarregarComprasImportadasAsync(cancellationToken);

        foreach (var compra in comprasImportadas)
        {
            if (compra.DataVencimento < dataInicial || compra.DataVencimento > dataFinal) continue;
            itens.Add(CriarItemCompraImportada(compra, compra.DataVencimento, DashboardCentralPrevisaoStatus.Substituido, pessoas));
        }

        if (incluirRecorrencias)
        {
            foreach (var proj in DashboardHelpers.ProjetarComprasImportadas(comprasImportadas, dataInicial, dataFinal, usarDataVencimento: true))
                itens.Add(CriarItemCompraImportada(proj.Compra, proj.Data, DashboardCentralPrevisaoStatus.Previsto, pessoas));
        }

        return itens;
    }

    private static PrevisaoItem CriarItemCompraImportada(
        ImportacaoCompraInfo compra, DateOnly data,
        DashboardCentralPrevisaoStatus status, IReadOnlyDictionary<Guid, string> pessoas)
    {
        return new PrevisaoItem(
            "ItemImportadoWhatsapp", compra.Id, data, compra.Tipo,
            compra.Recorrente ? DashboardCentralPrevisaoOrigem.CompraRecorrenteImportada : DashboardCentralPrevisaoOrigem.Parcela,
            status, compra.Descricao, compra.Valor,
            null,
            compra.ResponsavelId.HasValue ? pessoas.GetValueOrDefault(compra.ResponsavelId.Value) : null,
            compra.ContaGerencialId);
    }

    private static DashboardCentralPrevisaoOrigem ClassificarOrigem(Domain.Financeiro.OrigemLancamento origem, Guid? regraRecorrenciaId) =>
        origem == Domain.Financeiro.OrigemLancamento.Recorrencia
            ? DashboardCentralPrevisaoOrigem.ContaFuturaGerada
            : regraRecorrenciaId.HasValue
                ? DashboardCentralPrevisaoOrigem.Recorrencia
                : DashboardCentralPrevisaoOrigem.Parcela;
}
