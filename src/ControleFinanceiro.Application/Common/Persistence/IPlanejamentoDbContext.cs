using ControleFinanceiro.Domain.PlanejamentoCompras;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Common.Persistence;

public interface IPlanejamentoDbContext
{
    DbSet<PlanejamentoCompra> ComprasPlanejadas { get; }
}

public interface IReadOnlyPlanejamentoDbContext
{
    IQueryable<PlanejamentoCompra> ComprasPlanejadas { get; }
}
