using ControleFinanceiro.Domain.Anexos;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Common.Persistence;

public interface IAnexosDbContext
{
    DbSet<Anexo> Anexos { get; }
    DbSet<AnexoVinculo> AnexoVinculos { get; }
}
