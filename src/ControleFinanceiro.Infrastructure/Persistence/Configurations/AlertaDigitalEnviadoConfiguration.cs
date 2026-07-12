using ControleFinanceiro.Domain.FinanceAI;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControleFinanceiro.Infrastructure.Persistence.Configurations;

public sealed class AlertaDigitalEnviadoConfiguration : IEntityTypeConfiguration<AlertaDigitalEnviado>
{
    public void Configure(EntityTypeBuilder<AlertaDigitalEnviado> builder)
    {
        builder.ToTable("alertas_digitais_enviados");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Canal).HasMaxLength(20).IsRequired();
        builder.Property(x => x.TipoAlerta).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ChaveReferencia).HasMaxLength(200).IsRequired();
        builder.Property(x => x.DataEnvio).IsRequired();

        builder.HasIndex(x => new { x.UsuarioId, x.Canal, x.TipoAlerta, x.ChaveReferencia, x.DataEnvio })
            .IsUnique()
            .HasDatabaseName("IX_alertas_digitais_enviados_dedup");
    }
}
