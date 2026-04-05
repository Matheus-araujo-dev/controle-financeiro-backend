using ControleFinanceiro.Domain.Financeiro;
using ControleFinanceiro.Domain.ImportacoesWhatsapp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControleFinanceiro.Infrastructure.Persistence.Configurations;

public sealed class ItemImportadoWhatsappConfiguration : IEntityTypeConfiguration<ItemImportadoWhatsapp>
{
    public void Configure(EntityTypeBuilder<ItemImportadoWhatsapp> builder)
    {
        builder.ToTable("itens_importados_whatsapp");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TipoSugestao)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.PayloadSugeridoJson)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Observacao)
            .HasMaxLength(1000);

        builder.HasIndex(x => x.ImportacaoWhatsappId);
        builder.HasIndex(x => x.MovimentacaoFinanceiraId);

        builder.HasOne<MovimentacaoFinanceira>()
            .WithMany()
            .HasForeignKey(x => x.MovimentacaoFinanceiraId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
