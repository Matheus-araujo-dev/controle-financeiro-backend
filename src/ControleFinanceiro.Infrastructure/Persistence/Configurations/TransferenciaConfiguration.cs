using ControleFinanceiro.Domain.Cadastros.ContasBancarias;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControleFinanceiro.Infrastructure.Persistence.Configurations;

public sealed class TransferenciaConfiguration : IEntityTypeConfiguration<Transferencia>
{
    public void Configure(EntityTypeBuilder<Transferencia> builder)
    {
        builder.ToTable("transferencias");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Valor)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.Descricao)
            .HasMaxLength(500);

        builder.HasIndex(x => x.DataTransferencia);
        builder.HasIndex(x => x.ContaBancariaOrigemId);
        builder.HasIndex(x => x.ContaBancariaDestinoId);

        builder.HasOne<ContaBancaria>()
            .WithMany()
            .HasForeignKey(x => x.ContaBancariaOrigemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ContaBancaria>()
            .WithMany()
            .HasForeignKey(x => x.ContaBancariaDestinoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
