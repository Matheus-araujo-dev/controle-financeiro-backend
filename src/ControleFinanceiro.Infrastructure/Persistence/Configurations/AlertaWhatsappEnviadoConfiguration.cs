using ControleFinanceiro.Domain.FinanceAI;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControleFinanceiro.Infrastructure.Persistence.Configurations;

public sealed class AlertaWhatsappEnviadoConfiguration : IEntityTypeConfiguration<AlertaWhatsappEnviado>
{
    public void Configure(EntityTypeBuilder<AlertaWhatsappEnviado> builder)
    {
        builder.ToTable("whatsapp_alertas_enviados");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Telefone).HasMaxLength(30).IsRequired();
        builder.Property(x => x.TipoAlerta).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ChaveReferencia).HasMaxLength(200).IsRequired();
        builder.Property(x => x.DataEnvio).IsRequired();

        builder.HasIndex(x => new { x.Telefone, x.TipoAlerta, x.ChaveReferencia, x.DataEnvio })
            .IsUnique()
            .HasDatabaseName("IX_whatsapp_alertas_enviados_dedup");
    }
}
