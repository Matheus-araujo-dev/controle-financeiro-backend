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
        builder.Property(x => x.UpdatedAtUtc).IsConcurrencyToken();
        builder.Property(x => x.Descricao).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Documento).HasMaxLength(100);
        builder.Property(x => x.StatusItem).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(x => x.ConciliacaoId);
        builder.HasIndex(x => x.MovimentacaoVinculadaId);
        builder.Property(x => x.ChaveOrigem).HasMaxLength(200);
        builder.Property(x => x.ValorAnteriorSistema).HasPrecision(18, 2);
        builder.Property(x => x.NumeroParcela).HasDefaultValue(1);
        builder.Property(x => x.QuantidadeParcelas).HasDefaultValue(1);
        builder.HasIndex(x => new { x.ConciliacaoId, x.ChaveOrigem }).IsUnique();
        builder.HasIndex(x => new { x.ConciliacaoId, x.ContaPagarVinculadaId }).IsUnique();
        builder.HasOne<ControleFinanceiro.Domain.Financeiro.ContaPagar>().WithMany()
            .HasForeignKey(x => x.ContaPagarVinculadaId).OnDelete(DeleteBehavior.Restrict);
    }
}