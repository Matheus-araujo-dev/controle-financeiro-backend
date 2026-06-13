using ControleFinanceiro.Domain.FinanceAI;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControleFinanceiro.Infrastructure.Persistence.Configurations;

public sealed class AiConversaConfiguration : IEntityTypeConfiguration<AiConversa>
{
    public void Configure(EntityTypeBuilder<AiConversa> builder)
    {
        builder.ToTable("ai_conversas");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Canal)
            .IsRequired();

        builder.Property(x => x.ContatoExterno)
            .HasMaxLength(50);

        builder.HasMany(x => x.Mensagens)
            .WithOne()
            .HasForeignKey(x => x.ConversaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.ToolCalls)
            .WithOne()
            .HasForeignKey(x => x.ConversaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.FamiliaId);
        builder.HasIndex(x => x.UsuarioId);
    }
}
