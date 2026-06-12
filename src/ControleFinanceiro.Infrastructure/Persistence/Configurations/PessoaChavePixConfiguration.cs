using ControleFinanceiro.Domain.Cadastros.Pessoas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControleFinanceiro.Infrastructure.Persistence.Configurations;

public sealed class PessoaChavePixConfiguration : IEntityTypeConfiguration<PessoaChavePix>
{
    public void Configure(EntityTypeBuilder<PessoaChavePix> builder)
    {
        builder.ToTable("pessoas_chaves_pix");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Tipo)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Chave)
            .HasMaxLength(120)
            .IsRequired();

        builder.HasIndex(x => new { x.PessoaId, x.Tipo, x.Chave })
            .IsUnique();
    }
}
