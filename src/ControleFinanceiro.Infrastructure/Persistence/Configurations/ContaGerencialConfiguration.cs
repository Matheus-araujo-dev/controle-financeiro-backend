using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;
using ControleFinanceiro.Domain.Cadastros.Pessoas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControleFinanceiro.Infrastructure.Persistence.Configurations;

public sealed class ContaGerencialConfiguration : IEntityTypeConfiguration<ContaGerencial>
{
    public void Configure(EntityTypeBuilder<ContaGerencial> builder)
    {
        builder.ToTable("contas_gerenciais");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Codigo)
            .HasMaxLength(50);

        builder.Property(x => x.Descricao)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Tipo)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.EhPadraoRecebimentoFaturaCartao)
            .IsRequired();

        builder.HasIndex(x => x.ResponsavelPadraoId);
        builder.HasIndex(x => x.Codigo).IsUnique().HasFilter("\"Codigo\" IS NOT NULL");
        builder.HasIndex("Tipo", "Ativo");
        builder.HasIndex(x => x.ContaPaiId);

        builder.HasOne<ContaGerencial>()
            .WithMany()
            .HasForeignKey(x => x.ContaPaiId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Pessoa>()
            .WithMany()
            .HasForeignKey(x => x.ResponsavelPadraoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
