using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControleFinanceiro.Infrastructure.Persistence.Configurations;

public sealed class StatusMovimentacaoConfiguration : IEntityTypeConfiguration<StatusMovimentacao>
{
    public void Configure(EntityTypeBuilder<StatusMovimentacao> builder)
    {
        builder.ToTable("status_movimentacao");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Codigo)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Nome)
            .HasMaxLength(60)
            .IsRequired();

        builder.HasIndex(x => x.Codigo)
            .IsUnique();

        builder.HasData(StatusMovimentacao.Seeds());
    }
}
