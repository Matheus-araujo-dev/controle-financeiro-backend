using ControleFinanceiro.Domain.ImportacoesWhatsapp;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Common.Persistence;

public interface IImportacoesDbContext
{
    DbSet<ImportacaoWhatsapp> ImportacoesWhatsapp { get; }
    DbSet<ItemImportadoWhatsapp> ItensImportadosWhatsapp { get; }
}

public interface IReadOnlyImportacoesDbContext
{
    IQueryable<ImportacaoWhatsapp> ImportacoesWhatsapp { get; }
    IQueryable<ItemImportadoWhatsapp> ItensImportadosWhatsapp { get; }
}
