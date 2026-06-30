using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Contracts.Dashboard;
using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Dashboard;

public interface IDashboardContasGerenciaisService
{
    Task<DashboardContaGerencialResumoResponse> ObterContasGerenciaisResumoAsync(DashboardContaGerencialResumoQueryRequest query, CancellationToken cancellationToken);
    Task<DashboardContaGerencialSerieResponse> ObterContasGerenciaisSerieAsync(DashboardContaGerencialSerieQueryRequest query, CancellationToken cancellationToken);
    Task<DashboardContaGerencialLancamentosResponse> ObterContaGerencialLancamentosAsync(DashboardContaGerencialLancamentosQueryRequest query, CancellationToken cancellationToken);
}

public sealed class DashboardContasGerenciaisService(IAppDbContext dbContext, DashboardDbHelpers db) : IDashboardContasGerenciaisService
{
    public async Task<DashboardContaGerencialResumoResponse> ObterContasGerenciaisResumoAsync(
        DashboardContaGerencialResumoQueryRequest query, CancellationToken cancellationToken)
    {
        var tipoFiltro = DashboardHelpers.ParseTipoContaGerencial(query.Tipo);
        var (dataInicial, dias, _) = DashboardHelpers.ResolverJanela(query.MesReferencia, query.DataInicial, query.Dias);
        var dataFinal = dataInicial.AddDays(dias - 1);

        var rateios = await db.CarregarRateiosPorEmissaoAsync(dataInicial, dataFinal, cancellationToken);
        var contasGerenciais = await db.CarregarContasGerenciaisAsync(cancellationToken);

        var itens = rateios
            .GroupBy(r => r.ContaGerencialId)
            .Where(g => contasGerenciais.ContainsKey(g.Key))
            .Select(g =>
            {
                var cg = contasGerenciais[g.Key];
                return new DashboardContaGerencialResumoItemResponse(
                    g.Key, cg.Codigo, cg.Descricao, cg.Tipo.ToString(),
                    decimal.Round(g.Sum(r => r.ValorRateio), 2, MidpointRounding.AwayFromZero),
                    g.Select(r => r.LancamentoId).Distinct().Count(),
                    g.Max(r => r.DataEmissao));
            })
            .Where(item => !tipoFiltro.HasValue || item.Tipo == tipoFiltro.Value.ToString())
            .OrderByDescending(item => item.ValorTotal).ThenBy(item => item.Descricao)
            .ToList();

        var totalReceitas = decimal.Round(itens.Where(i => i.Tipo == nameof(TipoContaGerencial.Receita)).Sum(i => i.ValorTotal), 2, MidpointRounding.AwayFromZero);
        var totalDespesas = decimal.Round(itens.Where(i => i.Tipo == nameof(TipoContaGerencial.Despesa)).Sum(i => i.ValorTotal), 2, MidpointRounding.AwayFromZero);

        return new DashboardContaGerencialResumoResponse(
            dataInicial, dias, totalReceitas, totalDespesas,
            decimal.Round(totalReceitas - totalDespesas, 2, MidpointRounding.AwayFromZero), itens);
    }

    public async Task<DashboardContaGerencialSerieResponse> ObterContasGerenciaisSerieAsync(
        DashboardContaGerencialSerieQueryRequest query, CancellationToken cancellationToken)
    {
        var tipoFiltro = DashboardHelpers.ParseTipoContaGerencial(query.Tipo);
        var (dataInicial, dias, _) = DashboardHelpers.ResolverJanela(query.MesReferencia, query.DataInicial, query.Dias);
        var dataFinal = dataInicial.AddDays(dias - 1);

        var rateios = await db.CarregarRateiosPorEmissaoAsync(dataInicial, dataFinal, cancellationToken);
        var contasGerenciais = await db.CarregarContasGerenciaisAsync(cancellationToken);

        var rateiosFiltrados = rateios
            .Where(r => contasGerenciais.TryGetValue(r.ContaGerencialId, out var cg) &&
                        (!tipoFiltro.HasValue || cg.Tipo == tipoFiltro.Value) &&
                        (!query.ContaGerencialId.HasValue || r.ContaGerencialId == query.ContaGerencialId.Value))
            .ToList();

        var itens = new List<DashboardContaGerencialSerieDiaResponse>(dias);
        for (var i = 0; i < dias; i++)
        {
            var data = dataInicial.AddDays(i);
            var rateiosDia = rateiosFiltrados.Where(r => r.DataEmissao == data).ToList();
            var totalReceitas = decimal.Round(rateiosDia.Where(r => contasGerenciais[r.ContaGerencialId].Tipo == TipoContaGerencial.Receita).Sum(r => r.ValorRateio), 2, MidpointRounding.AwayFromZero);
            var totalDespesas = decimal.Round(rateiosDia.Where(r => contasGerenciais[r.ContaGerencialId].Tipo == TipoContaGerencial.Despesa).Sum(r => r.ValorRateio), 2, MidpointRounding.AwayFromZero);
            itens.Add(new DashboardContaGerencialSerieDiaResponse(data, totalReceitas, totalDespesas, decimal.Round(totalReceitas - totalDespesas, 2, MidpointRounding.AwayFromZero)));
        }

        return new DashboardContaGerencialSerieResponse(dataInicial, dias, query.Tipo, query.ContaGerencialId, itens);
    }

    public async Task<DashboardContaGerencialLancamentosResponse> ObterContaGerencialLancamentosAsync(
        DashboardContaGerencialLancamentosQueryRequest query, CancellationToken cancellationToken)
    {
        DashboardHelpers.ParseTipoContaGerencial(query.Tipo);

        if (!query.ContaGerencialId.HasValue || query.ContaGerencialId.Value == Guid.Empty)
            throw ValidationExceptionFactory.Create("ContaGerencialId", "Conta gerencial é obrigatória.");

        var (dataInicial, dias, _) = DashboardHelpers.ResolverJanela(query.MesReferencia, query.DataInicial, query.Dias);
        var dataFinal = dataInicial.AddDays(dias - 1);

        var contaGerencial = await dbContext.ContasGerenciais.AsNoTracking()
            .Where(c => c.Id == query.ContaGerencialId.Value)
            .Select(c => new ContaGerencialInfo(c.Codigo, c.Descricao, c.Tipo))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw ValidationExceptionFactory.Create("ContaGerencialId", "Conta gerencial não encontrada.");

        var pessoas = await db.CarregarPessoasAsync(cancellationToken);
        var rateios = await db.CarregarRateiosPorEmissaoAsync(dataInicial, dataFinal, cancellationToken);

        var itens = rateios
            .Where(r => r.ContaGerencialId == query.ContaGerencialId.Value)
            .OrderBy(r => r.DataEmissao).ThenBy(r => r.Descricao)
            .Select(r =>
            {
                var status = DashboardHelpers.StatusContasLookup.GetValueOrDefault(r.StatusContaId, new StatusContaInfo(string.Empty, string.Empty));
                return new DashboardContaGerencialLancamentoItemResponse(
                    r.LancamentoId, r.TipoLancamento, r.Descricao,
                    pessoas.GetValueOrDefault(r.PessoaId, string.Empty),
                    r.DataEmissao, r.DataVencimento, r.ValorLancamento, r.ValorRateio,
                    status.Codigo, status.Nome);
            })
            .ToList();

        return new DashboardContaGerencialLancamentosResponse(
            dataInicial, dias, query.Tipo!, query.ContaGerencialId.Value,
            contaGerencial.Codigo, contaGerencial.Descricao, itens);
    }
}
