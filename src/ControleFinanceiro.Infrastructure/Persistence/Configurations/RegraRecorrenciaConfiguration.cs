using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControleFinanceiro.Infrastructure.Persistence.Configurations;

public sealed class RegraRecorrenciaConfiguration : IEntityTypeConfiguration<RegraRecorrencia>
{
    public void Configure(EntityTypeBuilder<RegraRecorrencia> builder)
    {
        builder.ToTable("regras_recorrencia");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TipoLancamento)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.TipoPeriodicidade)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Observacao)
            .HasMaxLength(1000);

        builder.Property(x => x.TemplateJson)
            .IsRequired();

        builder.HasIndex(x => new { x.TipoLancamento, x.Ativa });
    }
}
