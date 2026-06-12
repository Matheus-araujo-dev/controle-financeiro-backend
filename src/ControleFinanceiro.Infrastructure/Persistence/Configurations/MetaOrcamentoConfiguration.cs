using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControleFinanceiro.Infrastructure.Persistence.Configurations;

public sealed class MetaOrcamentoConfiguration : IEntityTypeConfiguration<MetaOrcamento>
{
    public void Configure(EntityTypeBuilder<MetaOrcamento> builder)
    {
        builder.ToTable("metas_orcamento");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Competencia)
            .HasMaxLength(7)
            .IsRequired();

        builder.Property(x => x.ValorMeta)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.HasOne<ContaGerencial>()
            .WithMany()
            .HasForeignKey(x => x.ContaGerencialId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.FamiliaId, x.ContaGerencialId, x.Competencia })
            .IsUnique();
    }
}
