using ControleFinanceiro.Application.Common.Exceptions;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Contracts.Auth;
using ControleFinanceiro.Contracts.Familias;
using ControleFinanceiro.Domain.Identidade;
using ControleFinanceiro.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ControleFinanceiro.Application.Identidade;

public sealed class FamiliaAppService(
    IAppDbContext dbContext,
    ICurrentUser currentUser,
    ITokenService tokenService,
    IClock clock,
    IOptions<IdentidadeOptions> identidadeOptions,
    Cadastros.ContasGerenciais.ContasGerenciaisPadraoSeedService contasPadraoSeedService)
{
    private const int MaxParticipacoesPorUsuario = 3;

    public async Task<IReadOnlyList<ParticipacaoFamiliaResponse>> ListarMinhasFamiliasAsync(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out var usuarioId))
        {
            return [];
        }

        var familiaAtivaId = currentUser.WorkspaceId ?? currentUser.FamiliaId;

        return await (
            from m in dbContext.MembrosFamilia.AsNoTracking()
            join f in dbContext.Familias.AsNoTracking() on m.FamiliaId equals f.Id
            where m.UsuarioId == usuarioId
            orderby m.CreatedAtUtc
            select new ParticipacaoFamiliaResponse(
                f.Id,
                f.Nome,
                m.Papel.ToString(),
                familiaAtivaId.HasValue && f.Id == familiaAtivaId.Value))
            .ToListAsync(cancellationToken);
    }

    public async Task<FamiliaDetalheResponse?> ObterMinhaFamiliaAsync(CancellationToken cancellationToken)
    {
        var familiaId = currentUser.WorkspaceId ?? currentUser.FamiliaId;
        if (familiaId is null)
        {
            return null;
        }

        var familia = await dbContext.Familias
            .AsNoTracking()
            .SingleOrDefaultAsync(f => f.Id == familiaId.Value, cancellationToken);

        if (familia is null)
        {
            return null;
        }

        var membros = await dbContext.MembrosFamilia
            .AsNoTracking()
            .Where(m => m.FamiliaId == familiaId.Value)
            .Include(m => m.Usuario)
            .OrderBy(m => m.CreatedAtUtc)
            .Select(m => new MembroFamiliaResponse(
                m.Id,
                m.UsuarioId,
                m.Usuario!.Nome,
                m.Usuario!.Email,
                m.Usuario!.AvatarUrl,
                m.Papel.ToString()))
            .ToListAsync(cancellationToken);

        var utcNow = clock.UtcNow;
        var convitesPendentes = await dbContext.ConvitesFamilia
            .AsNoTracking()
            .Where(c => c.FamiliaId == familiaId.Value
                && c.Status == StatusConviteFamilia.Pendente
                && c.ExpiraEmUtc > utcNow)
            .OrderBy(c => c.CreatedAtUtc)
            .Select(c => new ConviteFamiliaResponse(
                c.Id,
                c.EmailConvidado,
                c.Papel.ToString(),
                c.Status.ToString(),
                c.ExpiraEmUtc))
            .ToListAsync(cancellationToken);

        return new FamiliaDetalheResponse(
            familia.Id,
            familia.Nome,
            currentUser.Papel ?? PapelFamilia.Membro.ToString(),
            membros,
            convitesPendentes);
    }

    public async Task<AuthTokenResponse> CriarWorkspaceAsync(string? nome, CancellationToken cancellationToken)
    {
        var usuario = await ExigirUsuarioAsync(cancellationToken);
        await ExigirLimiteParticipacoesDisponivelAsync(usuario.Id, cancellationToken);

        var nomeWorkspace = string.IsNullOrWhiteSpace(nome)
            ? $"Espaco de {usuario.Nome}"
            : nome.Trim();

        var familia = Familia.Criar(nomeWorkspace);
        dbContext.Familias.Add(familia);
        dbContext.MembrosFamilia.Add(MembroFamilia.Criar(familia.Id, usuario.Id, PapelFamilia.Administrador));
        usuario.DefinirFamiliaAtiva(familia.Id);

        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.DefinirWorkspaceCorrente(familia.Id);
        await contasPadraoSeedService.SeedAsync(cancellationToken);

        return await EmitirSessaoAsync(usuario, familia, PapelFamilia.Administrador, cancellationToken);
    }

    public async Task<FamiliaDetalheResponse?> RenomearAsync(string nome, CancellationToken cancellationToken)
    {
        var familiaId = ExigirFamiliaAdministrada();

        var familia = await dbContext.Familias
            .SingleOrDefaultAsync(f => f.Id == familiaId, cancellationToken);

        if (familia is null)
        {
            return null;
        }

        familia.Renomear(nome);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await ObterMinhaFamiliaAsync(cancellationToken);
    }

    public async Task<ConviteCriadoResponse> CriarConviteAsync(
        CriarConviteFamiliaRequest request,
        CancellationToken cancellationToken)
    {
        var familiaId = ExigirFamiliaAdministrada();
        var papel = ConverterPapel(request.Papel);
        var emailNormalizado = request.Email.Trim().ToLowerInvariant();

        var jaEhMembro = await dbContext.MembrosFamilia
            .AnyAsync(m => m.FamiliaId == familiaId && m.Usuario!.Email == emailNormalizado, cancellationToken);

        if (jaEhMembro)
        {
            throw new ApplicationValidationException(
                "Este e-mail já pertence a um membro da família.",
                new Dictionary<string, string[]> { ["email"] = ["E-mail já é membro da família."] });
        }

        var usuarioConvidado = await dbContext.Usuarios
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Email == emailNormalizado, cancellationToken);

        if (usuarioConvidado is not null)
        {
            await ExigirLimiteParticipacoesDisponivelAsync(usuarioConvidado.Id, cancellationToken);
        }

        var token = tokenService.GenerateOpaqueToken();
        var convite = ConviteFamilia.Criar(
            familiaId,
            emailNormalizado,
            papel,
            tokenService.HashToken(token),
            clock.UtcNow.AddHours(identidadeOptions.Value.ConviteExpiracaoHoras));

        dbContext.ConvitesFamilia.Add(convite);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ConviteCriadoResponse(
            convite.Id,
            convite.EmailConvidado,
            convite.Papel.ToString(),
            convite.ExpiraEmUtc,
            token);
    }

    public async Task<bool> RevogarConviteAsync(Guid conviteId, CancellationToken cancellationToken)
    {
        var familiaId = ExigirFamiliaAdministrada();

        var convite = await dbContext.ConvitesFamilia
            .SingleOrDefaultAsync(c => c.Id == conviteId && c.FamiliaId == familiaId, cancellationToken);

        if (convite is null)
        {
            return false;
        }

        convite.Revogar();
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ConviteDetalhePublicoResponse?> ObterConvitePorTokenAsync(
        string token,
        CancellationToken cancellationToken)
    {
        var convite = await BuscarConvitePorTokenAsync(token, cancellationToken);
        if (convite is null)
        {
            return null;
        }

        var familia = await dbContext.Familias
            .AsNoTracking()
            .SingleAsync(f => f.Id == convite.FamiliaId, cancellationToken);

        return new ConviteDetalhePublicoResponse(
            familia.Nome,
            convite.EmailConvidado,
            convite.Papel.ToString(),
            convite.EstaValido(clock.UtcNow));
    }

    public async Task<FamiliaDetalheResponse?> AceitarConviteAsync(string token, CancellationToken cancellationToken)
    {
        var usuario = await ExigirUsuarioAsync(cancellationToken);

        var convite = await BuscarConvitePorTokenAsync(token, cancellationToken, tracking: true);
        if (convite is null || !convite.EstaValido(clock.UtcNow))
        {
            return null;
        }

        var jaEhMembro = await dbContext.MembrosFamilia
            .AnyAsync(m => m.FamiliaId == convite.FamiliaId && m.UsuarioId == usuario.Id, cancellationToken);

        if (jaEhMembro)
        {
            throw new ApplicationValidationException(
                "Você já é membro desta família.",
                new Dictionary<string, string[]> { ["token"] = ["Usuário já é membro da família."] });
        }

        await ExigirLimiteParticipacoesDisponivelAsync(usuario.Id, cancellationToken);

        convite.Aceitar(usuario.Id, clock.UtcNow);
        dbContext.MembrosFamilia.Add(MembroFamilia.Criar(convite.FamiliaId, usuario.Id, convite.Papel));
        usuario.DefinirFamiliaAtiva(convite.FamiliaId);
        await dbContext.SaveChangesAsync(cancellationToken);

        var familia = await dbContext.Familias
            .AsNoTracking()
            .SingleAsync(f => f.Id == convite.FamiliaId, cancellationToken);

        return new FamiliaDetalheResponse(
            familia.Id,
            familia.Nome,
            convite.Papel.ToString(),
            [],
            []);
    }

    public async Task<AuthTokenResponse?> SelecionarFamiliaAtivaAsync(Guid familiaId, CancellationToken cancellationToken)
    {
        var usuario = await ExigirUsuarioAsync(cancellationToken);

        var membro = await dbContext.MembrosFamilia
            .AsNoTracking()
            .SingleOrDefaultAsync(m => m.FamiliaId == familiaId && m.UsuarioId == usuario.Id, cancellationToken);

        if (membro is null)
        {
            return null;
        }

        usuario.DefinirFamiliaAtiva(familiaId);
        var familia = await dbContext.Familias
            .AsNoTracking()
            .SingleAsync(f => f.Id == familiaId, cancellationToken);

        return await EmitirSessaoAsync(usuario, familia, membro.Papel, cancellationToken);
    }

    public async Task<bool> AlterarPapelMembroAsync(
        Guid membroId,
        string papel,
        CancellationToken cancellationToken)
    {
        var familiaId = ExigirFamiliaAdministrada();
        var novoPapel = ConverterPapel(papel);

        var membro = await dbContext.MembrosFamilia
            .SingleOrDefaultAsync(m => m.Id == membroId && m.FamiliaId == familiaId, cancellationToken);

        if (membro is null)
        {
            return false;
        }

        await GarantirOutroAdministradorAsync(membro, novoPapel, familiaId, cancellationToken);

        membro.AlterarPapel(novoPapel);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RemoverMembroAsync(Guid membroId, CancellationToken cancellationToken)
    {
        var familiaId = ExigirFamiliaAdministrada();

        var membro = await dbContext.MembrosFamilia
            .SingleOrDefaultAsync(m => m.Id == membroId && m.FamiliaId == familiaId, cancellationToken);

        if (membro is null)
        {
            return false;
        }

        await GarantirOutroAdministradorAsync(membro, novoPapel: null, familiaId, cancellationToken);

        dbContext.MembrosFamilia.Remove(membro);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task GarantirOutroAdministradorAsync(
        MembroFamilia membro,
        PapelFamilia? novoPapel,
        Guid familiaId,
        CancellationToken cancellationToken)
    {
        var deixaDeSerAdministrador = membro.Papel == PapelFamilia.Administrador
            && novoPapel != PapelFamilia.Administrador;

        if (!deixaDeSerAdministrador)
        {
            return;
        }

        var existeOutroAdministrador = await dbContext.MembrosFamilia
            .AnyAsync(
                m => m.FamiliaId == familiaId
                    && m.Id != membro.Id
                    && m.Papel == PapelFamilia.Administrador,
                cancellationToken);

        if (!existeOutroAdministrador)
        {
            throw new ApplicationValidationException(
                "A família precisa de ao menos um administrador.",
                new Dictionary<string, string[]> { ["papel"] = ["A família ficaria sem administrador."] });
        }
    }

    private async Task<ConviteFamilia?> BuscarConvitePorTokenAsync(
        string token,
        CancellationToken cancellationToken,
        bool tracking = false)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var tokenHash = tokenService.HashToken(token);
        var query = tracking
            ? dbContext.ConvitesFamilia
            : dbContext.ConvitesFamilia.AsNoTracking();

        return await query.SingleOrDefaultAsync(c => c.TokenHash == tokenHash, cancellationToken);
    }

    private Guid ExigirFamiliaAdministrada()
    {
        var familiaId = currentUser.FamiliaId
            ?? throw new AuthenticationFailedException("Usuário sem família associada.");

        if (!string.Equals(currentUser.Papel, PapelFamilia.Administrador.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            throw new ApplicationValidationException(
                "Apenas administradores do workspace podem executar esta acao.",
                new Dictionary<string, string[]> { ["papel"] = ["Permissão insuficiente."] });
        }

        return familiaId;
    }

    private async Task<Usuario> ExigirUsuarioAsync(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out var usuarioId))
        {
            throw new AuthenticationFailedException("Usuário autenticado não está registrado na plataforma.");
        }

        return await dbContext.Usuarios
            .SingleOrDefaultAsync(u => u.Id == usuarioId && u.Ativo, cancellationToken)
            ?? throw new AuthenticationFailedException("Usuário autenticado não está registrado na plataforma.");
    }

    private async Task ExigirLimiteParticipacoesDisponivelAsync(Guid usuarioId, CancellationToken cancellationToken)
    {
        var totalParticipacoes = await dbContext.MembrosFamilia
            .CountAsync(m => m.UsuarioId == usuarioId, cancellationToken);

        if (totalParticipacoes >= MaxParticipacoesPorUsuario)
        {
            throw new ApplicationValidationException(
                $"O usuário pode participar de no máximo {MaxParticipacoesPorUsuario} workspaces.",
                new Dictionary<string, string[]>
                {
                    ["workspace"] = [$"Limite máximo de {MaxParticipacoesPorUsuario} participações atingido."]
                });
        }
    }

    private async Task<AuthTokenResponse> EmitirSessaoAsync(
        Usuario usuario,
        Familia familia,
        PapelFamilia papel,
        CancellationToken cancellationToken)
    {
        var accessToken = tokenService.CreateAccessToken(usuario, familia.Id, papel);
        var refreshTokenValue = tokenService.GenerateOpaqueToken();

        dbContext.RefreshTokens.Add(RefreshToken.Criar(
            usuario.Id,
            tokenService.HashToken(refreshTokenValue),
            clock.UtcNow.Add(tokenService.RefreshTokenLifetime)));

        await dbContext.SaveChangesAsync(cancellationToken);

        return new AuthTokenResponse(
            accessToken.AccessToken,
            accessToken.ExpiresAtUtc,
            refreshTokenValue,
            new UsuarioAutenticadoResponse(
                usuario.Id,
                usuario.Email,
                usuario.Nome,
                usuario.AvatarUrl,
                new WorkspaceResumoResponse(familia.Id, familia.Nome, papel.ToString()),
                new FamiliaResumoResponse(familia.Id, familia.Nome, papel.ToString())));
    }

    private static PapelFamilia ConverterPapel(string papel)
    {
        if (!Enum.TryParse<PapelFamilia>(papel, ignoreCase: true, out var resultado))
        {
            throw new ApplicationValidationException(
                "Papel inválido.",
                new Dictionary<string, string[]> { ["papel"] = ["Use Administrador, Membro ou Visualizador."] });
        }

        return resultado;
    }
}



