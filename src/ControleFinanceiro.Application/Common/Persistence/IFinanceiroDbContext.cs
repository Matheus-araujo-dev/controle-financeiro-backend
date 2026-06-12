using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Common.Persistence;

public interface IFinanceiroDbContext
{
    DbSet<ContaPagar> ContasPagar { get; }
    DbSet<ContaReceber> ContasReceber { get; }
    DbSet<RateioContaGerencial> RateiosContaGerencial { get; }
    DbSet<MovimentacaoFinanceira> MovimentacoesFinanceiras { get; }
    DbSet<FaturaCartao> FaturasCartao { get; }
    DbSet<RegraRecorrencia> RegrasRecorrencia { get; }
}

public interface IReadOnlyFinanceiroDbContext
{
    IQueryable<ContaPagar> ContasPagar { get; }
    IQueryable<ContaReceber> ContasReceber { get; }
    IQueryable<RateioContaGerencial> RateiosContaGerencial { get; }
    IQueryable<MovimentacaoFinanceira> MovimentacoesFinanceiras { get; }
    IQueryable<FaturaCartao> FaturasCartao { get; }
    IQueryable<RegraRecorrencia> RegrasRecorrencia { get; }
}
