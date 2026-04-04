using ControleFinanceiro.Domain.Cadastros.Cartoes;
using ControleFinanceiro.Domain.Cadastros.ContasBancarias;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControleFinanceiro.Infrastructure.Persistence.Configurations;

public sealed class FaturaCartaoConfiguration : IEntityTypeConfiguration<FaturaCartao>
{
    public void Configure(EntityTypeBuilder<FaturaCartao> builder)
    {
        builder.ToTable("faturas_cartao");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Competencia)
            .HasMaxLength(7)
            .IsRequired();

        builder.Property(x => x.ValorTotal)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Observacao)
            .HasMaxLength(1000);

        builder.HasIndex(x => new { x.CartaoId, x.Competencia })
            .IsUnique();

        builder.HasOne<Cartao>()
            .WithMany()
            .HasForeignKey(x => x.CartaoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ContaBancaria>()
            .WithMany()
            .HasForeignKey(x => x.ContaBancariaPagamentoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
