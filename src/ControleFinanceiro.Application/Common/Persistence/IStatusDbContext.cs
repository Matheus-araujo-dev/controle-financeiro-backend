using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Common.Persistence;

public interface IStatusDbContext
{
    DbSet<StatusConta> StatusContas { get; }
    DbSet<StatusMovimentacao> StatusMovimentacoes { get; }
}

public interface IReadOnlyStatusDbContext
{
    IQueryable<StatusConta> StatusContas { get; }
    IQueryable<StatusMovimentacao> StatusMovimentacoes { get; }
}
