using ControleFinanceiro.Domain.Cadastros.Cartoes;
using ControleFinanceiro.Domain.Cadastros.ContasBancarias;
using ControleFinanceiro.Domain.Cadastros.FormasPagamento;
using ControleFinanceiro.Domain.Cadastros.Pessoas;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControleFinanceiro.Infrastructure.Persistence.Configurations;

public sealed class ContaReceberConfiguration : IEntityTypeConfiguration<ContaReceber>
{
    public void Configure(EntityTypeBuilder<ContaReceber> builder)
    {
        builder.ToTable("contas_receber");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.NumeroDocumento)
            .HasMaxLength(80);

        builder.Property(x => x.Descricao)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Observacao)
            .HasMaxLength(1000);

        builder.Property(x => x.Origem)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.ValorOriginal).HasPrecision(18, 2);
        builder.Property(x => x.ValorDesconto).HasPrecision(18, 2);
        builder.Property(x => x.ValorJuros).HasPrecision(18, 2);
        builder.Property(x => x.ValorMulta).HasPrecision(18, 2);
        builder.Property(x => x.ValorLiquido).HasPrecision(18, 2);

        builder.Ignore(x => x.Rateios);

        builder.HasIndex(x => x.DataVencimento);

        builder.HasOne<Pessoa>()
            .WithMany()
            .HasForeignKey(x => x.PagadorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Pessoa>()
            .WithMany()
            .HasForeignKey(x => x.ResponsavelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<FormaPagamento>()
            .WithMany()
            .HasForeignKey(x => x.FormaPagamentoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Cartao>()
            .WithMany()
            .HasForeignKey(x => x.CartaoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ContaBancaria>()
            .WithMany()
            .HasForeignKey(x => x.ContaBancariaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<StatusConta>()
            .WithMany()
            .HasForeignKey(x => x.StatusContaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
