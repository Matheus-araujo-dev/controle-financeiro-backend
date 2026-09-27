using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControleFinanceiro.Infrastructure.Persistence.Configurations;

public sealed class FechamentoMensalConfiguration : IEntityTypeConfiguration<FechamentoMensal>
{
    public void Configure(EntityTypeBuilder<FechamentoMensal> builder)
    {
        builder.ToTable("fechamentos_mensais");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UpdatedAtUtc).IsConcurrencyToken();
        builder.Property(x => x.Competencia).HasMaxLength(7).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.TotalReceitasSnapshot).HasPrecision(18, 2);
        builder.Property(x => x.TotalDespesasSnapshot).HasPrecision(18, 2);
        builder.Property(x => x.SaldoSnapshot).HasPrecision(18, 2);
        builder.Property(x => x.TotalPendenteSnapshot).HasPrecision(18, 2);
        builder.Property(x => x.TotalVencidoSnapshot).HasPrecision(18, 2);
        builder.Property(x => x.JustificativaReabertura).HasMaxLength(500);
        builder.HasIndex(x => new { x.FamiliaId, x.Competencia }).IsUnique();
    }
}
