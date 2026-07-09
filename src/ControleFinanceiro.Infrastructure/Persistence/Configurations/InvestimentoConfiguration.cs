using ControleFinanceiro.Domain.Cadastros.ContasBancarias;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControleFinanceiro.Infrastructure.Persistence.Configurations;

public sealed class InvestimentoConfiguration : IEntityTypeConfiguration<Investimento>
{
    public void Configure(EntityTypeBuilder<Investimento> builder)
    {
        builder.ToTable("investimentos");

        builder.Property(x => x.Nome).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Emissor).HasMaxLength(200);
        builder.Property(x => x.Tipo).IsRequired();
        builder.Property(x => x.Liquidez).IsRequired();
        builder.Property(x => x.ValorInvestido).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.ValorAtual).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.TaxaAnual).HasPrecision(10, 4);
        builder.Property(x => x.DataAplicacao).IsRequired();

        builder.Ignore(x => x.Rendimento);
        builder.Ignore(x => x.RendimentoPercent);

        builder.HasIndex(x => x.ContaBancariaVinculadaId);
        builder.HasIndex(x => x.FamiliaId);
        builder.HasIndex(x => new { x.FamiliaId, x.Tipo });

        builder.HasOne<ContaBancaria>()
            .WithMany()
            .HasForeignKey(x => x.ContaBancariaVinculadaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
