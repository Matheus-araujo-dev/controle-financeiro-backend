using ControleFinanceiro.Domain.Identidade;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControleFinanceiro.Infrastructure.Persistence.Configurations;

public sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuarios");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.GoogleSubject)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(x => x.GoogleSubject)
            .IsUnique();

        builder.Property(x => x.Email)
            .HasMaxLength(320)
            .IsRequired();

        builder.HasIndex(x => x.Email);

        builder.Property(x => x.Nome)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.AvatarUrl)
            .HasMaxLength(500);
    }
}

public sealed class FamiliaConfiguration : IEntityTypeConfiguration<Familia>
{
    public void Configure(EntityTypeBuilder<Familia> builder)
    {
        builder.ToTable("familias");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Nome)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasMany(x => x.Membros)
            .WithOne()
            .HasForeignKey(x => x.FamiliaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(Familia.Membros))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class MembroFamiliaConfiguration : IEntityTypeConfiguration<MembroFamilia>
{
    public void Configure(EntityTypeBuilder<MembroFamilia> builder)
    {
        builder.ToTable("membros_familia");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.FamiliaId, x.UsuarioId })
            .IsUnique();

        builder.Property(x => x.Papel)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.HasOne(x => x.Usuario)
            .WithMany()
            .HasForeignKey(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ConviteFamiliaConfiguration : IEntityTypeConfiguration<ConviteFamilia>
{
    public void Configure(EntityTypeBuilder<ConviteFamilia> builder)
    {
        builder.ToTable("convites_familia");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.EmailConvidado)
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(x => x.Papel)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.TokenHash)
            .HasMaxLength(128)
            .IsRequired();

        builder.HasIndex(x => x.TokenHash)
            .IsUnique();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.HasOne<Familia>()
            .WithMany()
            .HasForeignKey(x => x.FamiliaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TokenHash)
            .HasMaxLength(128)
            .IsRequired();

        builder.HasIndex(x => x.TokenHash)
            .IsUnique();

        builder.Property(x => x.SubstituidoPorTokenHash)
            .HasMaxLength(128);

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
