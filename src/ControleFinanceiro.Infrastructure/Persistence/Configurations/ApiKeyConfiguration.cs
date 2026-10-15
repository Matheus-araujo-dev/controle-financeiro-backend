using ControleFinanceiro.Domain.Identidade;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControleFinanceiro.Infrastructure.Persistence.Configurations;

public sealed class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.ToTable("api_keys");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Nome).HasMaxLength(200).IsRequired();
        builder.Property(x => x.KeyHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.KeyPrefix).HasMaxLength(12).IsRequired();
        builder.Property(x => x.FamiliaId).IsRequired();
        builder.Property(x => x.Papel).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Ativo).IsRequired();

        builder.HasIndex(x => x.KeyHash).IsUnique();
        builder.HasIndex(x => x.FamiliaId);
    }
}