using ControleFinanceiro.Domain.Anexos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControleFinanceiro.Infrastructure.Persistence.Configurations;

public sealed class AnexoConfiguration : IEntityTypeConfiguration<Anexo>
{
    public void Configure(EntityTypeBuilder<Anexo> builder)
    {
        builder.ToTable("anexos");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.NomeArquivoOriginal).HasMaxLength(255).IsRequired();
        builder.Property(x => x.CaminhoArquivo).HasMaxLength(500).IsRequired();
        builder.Property(x => x.MimeType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.HashSha256).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Origem).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.HashSha256);
        builder.HasIndex(x => x.ConversaAiId);
        builder.HasIndex(x => x.ImportacaoWhatsappId);
        builder.HasMany(x => x.Vinculos)
            .WithOne()
            .HasForeignKey(x => x.AnexoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
