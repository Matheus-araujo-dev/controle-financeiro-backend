using ControleFinanceiro.Domain.Conciliacao;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControleFinanceiro.Infrastructure.Persistence.Configurations;

public sealed class ConciliacaoConfiguration : IEntityTypeConfiguration<Conciliacao>
{
    public void Configure(EntityTypeBuilder<Conciliacao> builder)
    {
        builder.ToTable("conciliacoes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UpdatedAtUtc).IsConcurrencyToken();
        builder.Property(x => x.NomeArquivo).HasMaxLength(255).IsRequired();
        builder.Property(x => x.Formato).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(x => x.ContaBancariaId);
        builder.Property(x => x.HashArquivo).HasMaxLength(64);
        builder.HasIndex(x => new { x.FamiliaId, x.FaturaId, x.HashArquivo }).IsUnique();
        builder.HasOne<ControleFinanceiro.Domain.Financeiro.FaturaCartao>().WithMany()
            .HasForeignKey(x => x.FaturaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Itens)
            .WithOne()
            .HasForeignKey(x => x.ConciliacaoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}