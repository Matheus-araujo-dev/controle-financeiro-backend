using ControleFinanceiro.Domain.Financeiro;
using ControleFinanceiro.Domain.ImportacoesWhatsapp;
using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;
using ControleFinanceiro.Domain.Cadastros.Pessoas;
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

        builder.Property(x => x.ChaveAprendizado)
            .HasMaxLength(200);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Observacao)
            .HasMaxLength(1000);

        builder.Property(x => x.DescricaoAjustada)
            .HasMaxLength(200);

        builder.HasIndex(x => x.ImportacaoWhatsappId);
        builder.HasIndex(x => x.ChaveAprendizado);
        builder.HasIndex(x => x.ContaGerencialId);
        builder.HasIndex(x => x.ResponsavelId);
        builder.HasIndex(x => x.ContaReceberId);

        builder.HasOne<ContaGerencial>()
            .WithMany()
            .HasForeignKey(x => x.ContaGerencialId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Pessoa>()
            .WithMany()
            .HasForeignKey(x => x.ResponsavelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ContaReceber>()
            .WithMany()
            .HasForeignKey(x => x.ContaReceberId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
