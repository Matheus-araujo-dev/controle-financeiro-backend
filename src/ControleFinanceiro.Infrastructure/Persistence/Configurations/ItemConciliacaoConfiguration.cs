using ControleFinanceiro.Domain.Conciliacao;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControleFinanceiro.Infrastructure.Persistence.Configurations;

public sealed class ItemConciliacaoConfiguration : IEntityTypeConfiguration<ItemConciliacao>
{
    public void Configure(EntityTypeBuilder<ItemConciliacao> builder)
    {
        builder.ToTable("itens_conciliacao");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Descricao).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Documento).HasMaxLength(100);
        builder.Property(x => x.StatusItem).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(x => x.ConciliacaoId);
        builder.HasIndex(x => x.MovimentacaoVinculadaId);
    }
}