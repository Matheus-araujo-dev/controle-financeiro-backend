using ControleFinanceiro.Domain.FinanceAI;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControleFinanceiro.Infrastructure.Persistence.Configurations;

public sealed class ConfiguracaoNotificacaoConfiguration : IEntityTypeConfiguration<ConfiguracaoNotificacao>
{
    public void Configure(EntityTypeBuilder<ConfiguracaoNotificacao> builder)
    {
        builder.ToTable("configuracoes_notificacao");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.EmailDestinatario).HasMaxLength(200);

        builder.HasIndex(x => x.UsuarioId).IsUnique()
            .HasDatabaseName("IX_configuracoes_notificacao_usuario");

        builder.HasIndex(x => x.FamiliaId)
            .HasDatabaseName("IX_configuracoes_notificacao_familia");
    }
}
