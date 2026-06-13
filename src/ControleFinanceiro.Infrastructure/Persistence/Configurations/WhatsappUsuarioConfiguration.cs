using ControleFinanceiro.Domain.FinanceAI;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControleFinanceiro.Infrastructure.Persistence.Configurations;

public sealed class WhatsappUsuarioConfiguration : IEntityTypeConfiguration<WhatsappUsuario>
{
    public void Configure(EntityTypeBuilder<WhatsappUsuario> builder)
    {
        builder.ToTable("whatsapp_usuarios");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Telefone).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => x.Telefone);

        builder.Property(x => x.Ativo).IsRequired();
        builder.Property(x => x.VerificadoEm);

        builder.HasOne<ControleFinanceiro.Domain.Identidade.Usuario>()
            .WithMany()
            .HasForeignKey(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
