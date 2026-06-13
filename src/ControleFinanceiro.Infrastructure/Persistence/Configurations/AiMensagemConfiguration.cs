using ControleFinanceiro.Domain.FinanceAI;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControleFinanceiro.Infrastructure.Persistence.Configurations;

public sealed class AiMensagemConfiguration : IEntityTypeConfiguration<AiMensagem>
{
    public void Configure(EntityTypeBuilder<AiMensagem> builder)
    {
        builder.ToTable("ai_mensagens");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Papel)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Conteudo)
            .IsRequired();

        builder.Property(x => x.ExternalMessageId)
            .HasMaxLength(255);

        builder.HasIndex(x => x.ConversaId);
        builder.HasIndex(x => x.ExternalMessageId);
    }
}
