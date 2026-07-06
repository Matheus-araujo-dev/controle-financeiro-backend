using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;
using ControleFinanceiro.Domain.Cadastros.Pessoas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControleFinanceiro.Infrastructure.Persistence.Configurations;

public sealed class PessoaConfiguration : IEntityTypeConfiguration<Pessoa>
{
    public void Configure(EntityTypeBuilder<Pessoa> builder)
    {
        builder.ToTable("pessoas");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Nome)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.TipoPessoa)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.CpfCnpj)
            .HasMaxLength(20);

        builder.Property(x => x.Email)
            .HasMaxLength(200);

        builder.Property(x => x.Telefone)
            .HasMaxLength(50);

        builder.Property(x => x.Observacao)
            .HasMaxLength(1000);

        builder.Property(x => x.EhPagador).IsRequired().HasDefaultValue(true);
        builder.Property(x => x.EhRecebedor).IsRequired().HasDefaultValue(true);
        builder.Property(x => x.EhResponsavel).IsRequired().HasDefaultValue(true);

        builder.HasIndex(x => x.CpfCnpj)
            .IsUnique()
            .HasFilter("\"CpfCnpj\" IS NOT NULL");

        builder.HasIndex(x => x.Nome);
        builder.HasIndex("Ativo", "Nome");

        builder.Property(x => x.ContaGerencialDespesaId);
        builder.Property(x => x.ContaGerencialReceitaId);

        builder.HasOne<ContaGerencial>()
            .WithMany()
            .HasForeignKey(x => x.ContaGerencialDespesaId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne<ContaGerencial>()
            .WithMany()
            .HasForeignKey(x => x.ContaGerencialReceitaId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasMany(x => x.ChavesPix)
            .WithOne()
            .HasForeignKey(x => x.PessoaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
