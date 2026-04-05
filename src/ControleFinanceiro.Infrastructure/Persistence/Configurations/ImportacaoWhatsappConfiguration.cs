using ControleFinanceiro.Domain.ImportacoesWhatsapp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControleFinanceiro.Infrastructure.Persistence.Configurations;

public sealed class ImportacaoWhatsappConfiguration : IEntityTypeConfiguration<ImportacaoWhatsapp>
{
    public void Configure(EntityTypeBuilder<ImportacaoWhatsapp> builder)
    {
        builder.ToTable("importacoes_whatsapp");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TipoOrigem)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Remetente)
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(x => x.TextoBruto);

        builder.Property(x => x.NomeArquivo)
            .HasMaxLength(255);

        builder.Property(x => x.CaminhoArquivo)
            .HasMaxLength(500);

        builder.Property(x => x.MimeType)
            .HasMaxLength(100);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.ConfiancaExtracao)
            .HasPrecision(5, 4);

        builder.Property(x => x.MensagemErro)
            .HasMaxLength(1000);

        builder.HasIndex(x => x.Status);

        builder.HasMany(x => x.Itens)
            .WithOne()
            .HasForeignKey(x => x.ImportacaoWhatsappId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
