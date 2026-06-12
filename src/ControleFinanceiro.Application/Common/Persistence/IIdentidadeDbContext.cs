using ControleFinanceiro.Domain.Identidade;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Common.Persistence;

public interface IIdentidadeDbContext
{
    DbSet<Usuario> Usuarios { get; }

    DbSet<Familia> Familias { get; }

    DbSet<MembroFamilia> MembrosFamilia { get; }

    DbSet<ConviteFamilia> ConvitesFamilia { get; }

    DbSet<RefreshToken> RefreshTokens { get; }
}
