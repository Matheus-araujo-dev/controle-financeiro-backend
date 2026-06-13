using ControleFinanceiro.Domain.FinanceAI;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControleFinanceiro.Infrastructure.Persistence.Configurations;

public sealed class WhatsappConfigAlertaConfiguration : IEntityTypeConfiguration<WhatsappConfigAlerta>
{
    public void Configure(EntityTypeBuilder<WhatsappConfigAlerta> builder)
    {
        builder.ToTable("whatsapp_config_alertas");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DiasAntecedenciaVencimento).IsRequired();
        builder.Property(x => x.ReceberVencimento).IsRequired();
        builder.Property(x => x.ReceberLimiteCategoria).IsRequired();
        builder.Property(x => x.ReceberLimiteResponsavel).IsRequired();

        builder.HasOne<ControleFinanceiro.Domain.Identidade.Usuario>()
            .WithMany()
            .HasForeignKey(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
