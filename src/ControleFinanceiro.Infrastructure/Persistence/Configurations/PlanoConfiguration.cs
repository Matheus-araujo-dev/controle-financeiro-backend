using ControleFinanceiro.Domain.Cadastros.ContasBancarias;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControleFinanceiro.Infrastructure.Persistence.Configurations;

public sealed class PlanoConfiguration : IEntityTypeConfiguration<Plano>
{
    public void Configure(EntityTypeBuilder<Plano> builder)
    {
        builder.ToTable("planos");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Nome)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Descricao)
            .HasMaxLength(500);

        builder.Property(x => x.ValorMensal)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.TotalRetirado)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Ignore(x => x.ValorTotal);
        builder.Ignore(x => x.TotalAcumulado);
        builder.Ignore(x => x.Concluido);

        builder.HasIndex(x => x.ContaBancariaCaixaId);
        builder.HasIndex(x => x.FamiliaId);

        builder.HasOne<ContaBancaria>()
            .WithMany()
            .HasForeignKey(x => x.ContaBancariaCaixaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
