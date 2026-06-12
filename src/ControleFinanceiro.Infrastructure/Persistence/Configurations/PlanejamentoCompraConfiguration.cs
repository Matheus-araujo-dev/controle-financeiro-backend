using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;
using ControleFinanceiro.Domain.Cadastros.Pessoas;
using ControleFinanceiro.Domain.Financeiro;
using ControleFinanceiro.Domain.PlanejamentoCompras;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControleFinanceiro.Infrastructure.Persistence.Configurations;

public sealed class PlanejamentoCompraConfiguration : IEntityTypeConfiguration<PlanejamentoCompra>
{
    public void Configure(EntityTypeBuilder<PlanejamentoCompra> builder)
    {
        builder.ToTable("compras_planejadas");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Titulo)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.Descricao)
            .HasMaxLength(500);

        builder.Property(x => x.ValorEstimado)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(x => x.DataDesejada)
            .HasColumnType("date");

        builder.Property(x => x.Prioridade)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Observacao)
            .HasMaxLength(500);

        builder.Property(x => x.Link)
            .HasMaxLength(500);

        builder.HasOne<Pessoa>()
            .WithMany()
            .HasForeignKey(x => x.ResponsavelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ContaGerencial>()
            .WithMany()
            .HasForeignKey(x => x.ContaGerencialId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ContaPagar>()
            .WithMany()
            .HasForeignKey(x => x.ContaPagarGeradaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
