using ControleFinanceiro.Domain.Cadastros.Cartoes;
using ControleFinanceiro.Domain.Cadastros.ContasBancarias;
using ControleFinanceiro.Domain.Cadastros.FormasPagamento;
using ControleFinanceiro.Domain.Cadastros.Pessoas;
using ControleFinanceiro.Domain.Financeiro;
using ControleFinanceiro.Domain.ImportacoesWhatsapp;
using ControleFinanceiro.Domain.PlanejamentoCompras;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControleFinanceiro.Infrastructure.Persistence.Configurations;

public sealed class ContaPagarConfiguration : IEntityTypeConfiguration<ContaPagar>
{
    public void Configure(EntityTypeBuilder<ContaPagar> builder)
    {
        builder.ToTable("contas_pagar");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.NumeroDocumento)
            .HasMaxLength(80);

        builder.Property(x => x.Descricao)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Observacao)
            .HasMaxLength(1000);

        builder.Property(x => x.ChaveSerieImportacaoCartao)
            .HasMaxLength(180);

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
        builder.HasIndex(x => x.DataEmissao);
        builder.HasIndex(x => x.RegraRecorrenciaId);
        builder.HasIndex(x => x.ResponsavelCompraId);
        builder.HasIndex(x => x.GrupoParcelamentoId);

        builder.HasIndex("StatusContaId", "DataVencimento");
        builder.HasIndex("RecebedorId", "StatusContaId");
        builder.HasIndex(x => x.OrigemImportacaoWhatsappId);
        builder.HasIndex(x => new { x.CartaoId, x.ChaveSerieImportacaoCartao, x.NumeroParcela, x.QuantidadeParcelas })
            .HasFilter("\"CartaoId\" IS NOT NULL AND \"ChaveSerieImportacaoCartao\" IS NOT NULL");
        builder.HasIndex(x => x.Descricao);
        builder.HasIndex(x => x.NumeroDocumento);

        builder.HasOne<Pessoa>()
            .WithMany()
            .HasForeignKey(x => x.RecebedorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Pessoa>()
            .WithMany()
            .HasForeignKey(x => x.ResponsavelCompraId)
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

        builder.HasOne<RegraRecorrencia>()
            .WithMany()
            .HasForeignKey(x => x.RegraRecorrenciaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PlanejamentoCompra>()
            .WithMany()
            .HasForeignKey(x => x.OrigemCompraPlanejadaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ImportacaoWhatsapp>()
            .WithMany()
            .HasForeignKey(x => x.OrigemImportacaoWhatsappId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<FaturaCartao>()
            .WithMany()
            .HasForeignKey(x => x.FaturaCartaoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
