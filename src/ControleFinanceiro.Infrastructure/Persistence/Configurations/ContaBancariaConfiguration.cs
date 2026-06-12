using ControleFinanceiro.Domain.Cadastros.ContasBancarias;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControleFinanceiro.Infrastructure.Persistence.Configurations;

public sealed class ContaBancariaConfiguration : IEntityTypeConfiguration<ContaBancaria>
{
    public void Configure(EntityTypeBuilder<ContaBancaria> builder)
    {
        builder.ToTable("contas_bancarias");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Nome)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.Banco)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.Agencia)
            .HasMaxLength(50);

        builder.Property(x => x.NumeroConta)
            .HasMaxLength(50);

        builder.Property(x => x.TipoConta)
            .HasMaxLength(50);

        builder.Property(x => x.SaldoInicial)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(x => x.DataSaldoInicial)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(x => x.LimiteCartoesCompartilhado)
            .HasColumnType("decimal(18,2)");
    }
}
