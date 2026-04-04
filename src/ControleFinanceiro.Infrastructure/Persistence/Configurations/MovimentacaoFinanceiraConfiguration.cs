using ControleFinanceiro.Domain.Cadastros.ContasBancarias;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControleFinanceiro.Infrastructure.Persistence.Configurations;

public sealed class MovimentacaoFinanceiraConfiguration : IEntityTypeConfiguration<MovimentacaoFinanceira>
{
    public void Configure(EntityTypeBuilder<MovimentacaoFinanceira> builder)
    {
        builder.ToTable("movimentacoes_financeiras");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Tipo)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Natureza)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Valor)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.Observacao)
            .HasMaxLength(1000);

        builder.HasIndex(x => x.DataMovimentacao);

        builder.HasOne<ContaBancaria>()
            .WithMany()
            .HasForeignKey(x => x.ContaBancariaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ContaPagar>()
            .WithMany()
            .HasForeignKey(x => x.ContaPagarId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ContaReceber>()
            .WithMany()
            .HasForeignKey(x => x.ContaReceberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<StatusMovimentacao>()
            .WithMany()
            .HasForeignKey(x => x.StatusMovimentacaoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
