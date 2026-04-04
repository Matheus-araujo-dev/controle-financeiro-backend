using ControleFinanceiro.Domain.Cadastros.Cartoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControleFinanceiro.Infrastructure.Persistence.Configurations;

public sealed class CartaoConfiguration : IEntityTypeConfiguration<Cartao>
{
    public void Configure(EntityTypeBuilder<Cartao> builder)
    {
        builder.ToTable("cartoes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Nome)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.Bandeira)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.NumeroFinal)
            .HasMaxLength(4)
            .IsRequired();

        builder.Property(x => x.LimiteCredito)
            .HasColumnType("decimal(18,2)");

        builder.HasOne<ControleFinanceiro.Domain.Cadastros.ContasBancarias.ContaBancaria>()
            .WithMany()
            .HasForeignKey(x => x.ContaBancariaPagamentoPadraoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
