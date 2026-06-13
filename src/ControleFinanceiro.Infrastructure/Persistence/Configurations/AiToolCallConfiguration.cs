using ControleFinanceiro.Domain.FinanceAI;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControleFinanceiro.Infrastructure.Persistence.Configurations;

public sealed class AiToolCallConfiguration : IEntityTypeConfiguration<AiToolCall>
{
    public void Configure(EntityTypeBuilder<AiToolCall> builder)
    {
        builder.ToTable("ai_tool_calls");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.NomeFerramenta)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(x => x.ConversaId);
    }
}
