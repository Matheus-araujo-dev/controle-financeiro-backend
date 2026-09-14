using ControleFinanceiro.Domain.Conciliacao;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Common.Persistence;

public interface IConciliacaoDbContext
{
    DbSet<Domain.Conciliacao.Conciliacao> Conciliacoes { get; }

    DbSet<ItemConciliacao> ItensConciliacao { get; }
}