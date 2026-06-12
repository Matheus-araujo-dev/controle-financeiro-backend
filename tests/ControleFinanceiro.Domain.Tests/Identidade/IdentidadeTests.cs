using ControleFinanceiro.Domain.Identidade;
using FluentAssertions;

namespace ControleFinanceiro.Domain.Tests.Identidade;

public sealed class UsuarioTests
{
    [Fact]
    public void Criar_DevePreencherDadosENormalizar()
    {
        var usuario = Usuario.Criar("google-sub-1", "  USER@example.com  ", "  Maria  ", "  http://avatar  ");

        usuario.GoogleSubject.Should().Be("google-sub-1");
        usuario.Email.Should().Be("USER@example.com");
        usuario.Nome.Should().Be("Maria");
        usuario.AvatarUrl.Should().Be("http://avatar");
        usuario.Ativo.Should().BeTrue();
        usuario.FamiliaAtivaId.Should().BeNull();
    }

    [Fact]
    public void Criar_SemSubject_DeveFalhar()
    {
        var act = () => Usuario.Criar(" ", "user@example.com", "Maria", null);
        act.Should().Throw<ArgumentException>().WithParameterName("googleSubject");
    }

    [Fact]
    public void Criar_SemEmail_DeveFalhar()
    {
        var act = () => Usuario.Criar("sub", " ", "Maria", null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AtualizarPerfil_SemNome_DeveUsarEmail()
    {
        var usuario = Usuario.Criar("sub", "user@example.com", " ", null);
        usuario.Nome.Should().Be("user@example.com");
    }

    [Fact]
    public void DefinirFamiliaAtiva_DeveAtualizar()
    {
        var usuario = Usuario.Criar("sub", "user@example.com", "Maria", null);
        var familiaId = Guid.NewGuid();

        usuario.DefinirFamiliaAtiva(familiaId);

        usuario.FamiliaAtivaId.Should().Be(familiaId);
    }

    [Fact]
    public void DefinirFamiliaAtiva_ComGuidVazio_DeveFalhar()
    {
        var usuario = Usuario.Criar("sub", "user@example.com", "Maria", null);
        var act = () => usuario.DefinirFamiliaAtiva(Guid.Empty);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Desativar_DeveMarcarInativo()
    {
        var usuario = Usuario.Criar("sub", "user@example.com", "Maria", null);
        usuario.Desativar();
        usuario.Ativo.Should().BeFalse();
    }
}

public sealed class FamiliaTests
{
    [Fact]
    public void Criar_DevePreencherNome()
    {
        var familia = Familia.Criar("  Família Silva  ");

        familia.Nome.Should().Be("Família Silva");
        familia.Ativa.Should().BeTrue();
        familia.Membros.Should().BeEmpty();
    }

    [Fact]
    public void Criar_SemNome_DeveFalhar()
    {
        var act = () => Familia.Criar(" ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AdicionarMembro_DeveVincularUsuario()
    {
        var familia = Familia.Criar("Família Silva");
        var usuarioId = Guid.NewGuid();

        var membro = familia.AdicionarMembro(usuarioId, PapelFamilia.Administrador);

        membro.FamiliaId.Should().Be(familia.Id);
        membro.UsuarioId.Should().Be(usuarioId);
        membro.Papel.Should().Be(PapelFamilia.Administrador);
        familia.Membros.Should().ContainSingle();
    }

    [Fact]
    public void AdicionarMembro_Duplicado_DeveFalhar()
    {
        var familia = Familia.Criar("Família Silva");
        var usuarioId = Guid.NewGuid();
        familia.AdicionarMembro(usuarioId, PapelFamilia.Membro);

        var act = () => familia.AdicionarMembro(usuarioId, PapelFamilia.Visualizador);

        act.Should().Throw<InvalidOperationException>();
    }
}

public sealed class MembroFamiliaTests
{
    [Fact]
    public void Criar_ComFamiliaVazia_DeveFalhar()
    {
        var act = () => MembroFamilia.Criar(Guid.Empty, Guid.NewGuid(), PapelFamilia.Membro);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Criar_ComUsuarioVazio_DeveFalhar()
    {
        var act = () => MembroFamilia.Criar(Guid.NewGuid(), Guid.Empty, PapelFamilia.Membro);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AlterarPapel_DeveAtualizar()
    {
        var membro = MembroFamilia.Criar(Guid.NewGuid(), Guid.NewGuid(), PapelFamilia.Membro);
        membro.AlterarPapel(PapelFamilia.Administrador);
        membro.Papel.Should().Be(PapelFamilia.Administrador);
    }
}

public sealed class ConviteFamiliaTests
{
    private static readonly DateTime UtcNow = new(2026, 6, 11, 12, 0, 0, DateTimeKind.Utc);

    private static ConviteFamilia CriarConvite(DateTime? expiraEm = null) =>
        ConviteFamilia.Criar(
            Guid.NewGuid(),
            "Convidado@Email.com",
            PapelFamilia.Membro,
            "hash",
            expiraEm ?? UtcNow.AddHours(72));

    [Fact]
    public void Criar_DeveNormalizarEmailEDefinirPendente()
    {
        var convite = CriarConvite();

        convite.EmailConvidado.Should().Be("convidado@email.com");
        convite.Status.Should().Be(StatusConviteFamilia.Pendente);
        convite.EstaValido(UtcNow).Should().BeTrue();
    }

    [Fact]
    public void EstaValido_Expirado_DeveSerFalso()
    {
        var convite = CriarConvite(UtcNow.AddHours(-1));
        convite.EstaValido(UtcNow).Should().BeFalse();
    }

    [Fact]
    public void Aceitar_DeveRegistrarUsuarioEData()
    {
        var convite = CriarConvite();
        var usuarioId = Guid.NewGuid();

        convite.Aceitar(usuarioId, UtcNow);

        convite.Status.Should().Be(StatusConviteFamilia.Aceito);
        convite.UsuarioAceiteId.Should().Be(usuarioId);
        convite.AceitoEmUtc.Should().Be(UtcNow);
    }

    [Fact]
    public void Aceitar_Expirado_DeveFalhar()
    {
        var convite = CriarConvite(UtcNow.AddHours(-1));
        var act = () => convite.Aceitar(Guid.NewGuid(), UtcNow);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Revogar_Pendente_DeveAtualizar()
    {
        var convite = CriarConvite();
        convite.Revogar();
        convite.Status.Should().Be(StatusConviteFamilia.Revogado);
    }

    [Fact]
    public void Revogar_Aceito_DeveFalhar()
    {
        var convite = CriarConvite();
        convite.Aceitar(Guid.NewGuid(), UtcNow);

        var act = () => convite.Revogar();

        act.Should().Throw<InvalidOperationException>();
    }
}

public sealed class RefreshTokenTests
{
    private static readonly DateTime UtcNow = new(2026, 6, 11, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Criar_DeveEstarAtivoAteExpirar()
    {
        var token = RefreshToken.Criar(Guid.NewGuid(), "hash", UtcNow.AddDays(30));

        token.EstaAtivo(UtcNow).Should().BeTrue();
        token.EstaAtivo(UtcNow.AddDays(31)).Should().BeFalse();
    }

    [Fact]
    public void Criar_SemUsuario_DeveFalhar()
    {
        var act = () => RefreshToken.Criar(Guid.Empty, "hash", UtcNow);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Revogar_DeveInativarERegistrarSubstituto()
    {
        var token = RefreshToken.Criar(Guid.NewGuid(), "hash", UtcNow.AddDays(30));

        token.Revogar(UtcNow, "novo-hash");

        token.EstaAtivo(UtcNow).Should().BeFalse();
        token.RevogadoEmUtc.Should().Be(UtcNow);
        token.SubstituidoPorTokenHash.Should().Be("novo-hash");
    }
}
