using ControleFinanceiro.Domain.Conciliacao;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControleFinanceiro.Infrastructure.Persistence.Configurations;

public sealed class MemoriaEstabelecimentoConfiguration : IEntityTypeConfiguration<MemoriaEstabelecimento>
{
    public void Configure(EntityTypeBuilder<MemoriaEstabelecimento> builder)
    {
        builder.ToTable("memorias_estabelecimento");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Chave).HasMaxLength(500).IsRequired();
        builder.Property(x => x.PreferenciasJson).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsConcurrencyToken();
        builder.HasIndex(x => new { x.FamiliaId, x.CartaoId, x.Chave }).IsUnique();
        builder.HasOne<ControleFinanceiro.Domain.Cadastros.Cartoes.Cartao>().WithMany().HasForeignKey(x => x.CartaoId).OnDelete(DeleteBehavior.Restrict);
    }
}
