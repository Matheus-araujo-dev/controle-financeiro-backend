using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControleFinanceiro.Infrastructure.Persistence.Configurations;

public sealed class StatusContaConfiguration : IEntityTypeConfiguration<StatusConta>
{
    public void Configure(EntityTypeBuilder<StatusConta> builder)
    {
        builder.ToTable("status_conta");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Codigo)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Nome)
            .HasMaxLength(60)
            .IsRequired();

        builder.HasIndex(x => x.Codigo)
            .IsUnique();

        builder.HasData(StatusConta.Seeds());
    }
}
