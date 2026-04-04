using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControleFinanceiro.Infrastructure.Persistence.Configurations;

public sealed class RateioContaGerencialConfiguration : IEntityTypeConfiguration<RateioContaGerencial>
{
    public void Configure(EntityTypeBuilder<RateioContaGerencial> builder)
    {
        builder.ToTable("rateios_conta_gerencial");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TipoLancamento)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Percentual)
            .HasPrecision(9, 4);

        builder.Property(x => x.Valor)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.HasOne<ContaPagar>()
            .WithMany()
            .HasForeignKey(x => x.ContaPagarId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ContaReceber>()
            .WithMany()
            .HasForeignKey(x => x.ContaReceberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ContaGerencial>()
            .WithMany()
            .HasForeignKey(x => x.ContaGerencialId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
