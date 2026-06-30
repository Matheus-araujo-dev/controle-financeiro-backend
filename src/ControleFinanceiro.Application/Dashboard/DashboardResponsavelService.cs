using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Contracts.Dashboard;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Dashboard;

public interface IDashboardResponsavelService
{
    Task<DashboardResponsavelResumoResponse> ObterResumoPorResponsavelAsync(DashboardResponsavelQueryRequest query, CancellationToken cancellationToken);
}

public sealed class DashboardResponsavelService(IAppDbContext dbContext, DashboardDbHelpers db) : IDashboardResponsavelService
{
    public async Task<DashboardResponsavelResumoResponse> ObterResumoPorResponsavelAsync(
        DashboardResponsavelQueryRequest query, CancellationToken cancellationToken)
    {
        var (dataInicial, dias, _) = DashboardHelpers.ResolverJanela(query.MesReferencia, query.DataInicial, query.Dias);
        var dataFinal = dataInicial.AddDays(dias - 1);
        var pessoas = await db.CarregarPessoasAsync(cancellationToken);

        var despesas = await dbContext.ContasPagar.AsNoTracking()
            .Where(c => c.StatusContaId != StatusConta.CanceladaId &&
                        c.DataEmissao >= dataInicial && c.DataEmissao <= dataFinal)
            .GroupBy(c => c.ResponsavelCompraId)
            .Select(g => new
            {
                ResponsavelId = g.Key,
                Total = g.Sum(c => c.ValorLiquido),
                TotalCartao = g.Sum(c => c.CartaoId.HasValue ? c.ValorLiquido : 0m),
                Quantidade = g.Count()
            })
            .ToListAsync(cancellationToken);

        var receitas = await dbContext.ContasReceber.AsNoTracking()
            .Where(c => c.StatusContaId != StatusConta.CanceladaId &&
                        c.DataEmissao >= dataInicial && c.DataEmissao <= dataFinal)
            .GroupBy(c => c.ResponsavelId)
            .Select(g => new
            {
                ResponsavelId = g.Key,
                Total = g.Sum(c => c.ValorLiquido),
                Quantidade = g.Count()
            })
            .ToListAsync(cancellationToken);

        var porResponsavel = new Dictionary<Guid, (decimal Despesas, decimal Cartao, decimal Receitas, int Quantidade)>();

        foreach (var grupo in despesas)
            porResponsavel[grupo.ResponsavelId ?? Guid.Empty] = (grupo.Total, grupo.TotalCartao, 0m, grupo.Quantidade);

        foreach (var grupo in receitas)
        {
            var chave = grupo.ResponsavelId ?? Guid.Empty;
            var atual = porResponsavel.GetValueOrDefault(chave);
            porResponsavel[chave] = (atual.Despesas, atual.Cartao, atual.Receitas + grupo.Total, atual.Quantidade + grupo.Quantidade);
        }

        var itens = porResponsavel
            .Select(par => new DashboardResponsavelItemResponse(
                par.Key == Guid.Empty ? null : par.Key,
                par.Key == Guid.Empty ? "Sem responsável" : pessoas.GetValueOrDefault(par.Key, "Pessoa removida"),
                decimal.Round(par.Value.Despesas, 2, MidpointRounding.AwayFromZero),
                decimal.Round(par.Value.Cartao, 2, MidpointRounding.AwayFromZero),
                decimal.Round(par.Value.Receitas, 2, MidpointRounding.AwayFromZero),
                decimal.Round(par.Value.Receitas - par.Value.Despesas, 2, MidpointRounding.AwayFromZero),
                par.Value.Quantidade))
            .OrderByDescending(item => item.TotalDespesas)
            .ToList();

        return new DashboardResponsavelResumoResponse(
            dataInicial, dias,
            decimal.Round(itens.Sum(item => item.TotalDespesas), 2, MidpointRounding.AwayFromZero),
            decimal.Round(itens.Sum(item => item.TotalReceitas), 2, MidpointRounding.AwayFromZero),
            itens);
    }
}
