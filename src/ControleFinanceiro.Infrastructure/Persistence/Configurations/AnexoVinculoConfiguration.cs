using ControleFinanceiro.Domain.Anexos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControleFinanceiro.Infrastructure.Persistence.Configurations;

public sealed class AnexoVinculoConfiguration : IEntityTypeConfiguration<AnexoVinculo>
{
    public void Configure(EntityTypeBuilder<AnexoVinculo> builder)
    {
        builder.ToTable("anexo_vinculos");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TipoEntidade).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.HasIndex(x => new { x.AnexoId, x.TipoEntidade, x.EntidadeId }).IsUnique();
        builder.HasIndex(x => new { x.TipoEntidade, x.EntidadeId });
    }
}
