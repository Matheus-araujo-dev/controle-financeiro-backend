using ControleFinanceiro.Application.Common.Exceptions;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Contracts.Auth;
using ControleFinanceiro.Domain.Identidade;
using ControleFinanceiro.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ControleFinanceiro.Application.Identidade;

public sealed class AuthAppService(
    IAppDbContext dbContext,
    IGoogleTokenValidator googleTokenValidator,
    ITokenService tokenService,
    IClock clock,
    IOptions<IdentidadeOptions> identidadeOptions)
{
    public async Task<AuthTokenResponse> LoginComGoogleAsync(string idToken, CancellationToken cancellationToken)
    {
        var googleUser = await googleTokenValidator.ValidateAsync(idToken, cancellationToken);

        var usuario = await dbContext.Usuarios
            .SingleOrDefaultAsync(u => u.GoogleSubject == googleUser.Subject, cancellationToken);

        if (usuario is null)
        {
            usuario = Usuario.Criar(googleUser.Subject, googleUser.Email, googleUser.Nome, googleUser.AvatarUrl);
            dbContext.Usuarios.Add(usuario);
        }
        else
        {
            if (!usuario.Ativo)
            {
                throw new AuthenticationFailedException("Usuário desativado.");
            }

            usuario.AtualizarPerfil(googleUser.Email, googleUser.Nome, googleUser.AvatarUrl);
        }

        var (membro, familia) = await GarantirMembroFamiliaAsync(usuario, cancellationToken);
        usuario.DefinirFamiliaAtiva(membro.FamiliaId);

        return await EmitirTokensAsync(usuario, familia, membro.Papel, cancellationToken);
    }

    public async Task<AuthTokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var utcNow = clock.UtcNow;
        var tokenHash = tokenService.HashToken(refreshToken);

        var tokenAtual = await dbContext.RefreshTokens
            .SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (tokenAtual is null || !tokenAtual.EstaAtivo(utcNow))
        {
            throw new AuthenticationFailedException("Sessão expirada. Faça login novamente.");
        }

        var usuario = await dbContext.Usuarios
            .SingleOrDefaultAsync(u => u.Id == tokenAtual.UsuarioId && u.Ativo, cancellationToken)
            ?? throw new AuthenticationFailedException("Usuário desativado.");

        var membro = await ObterMembroAtivoAsync(usuario, cancellationToken)
            ?? throw new AuthenticationFailedException("Usuário sem família associada.");

        var familia = await dbContext.Familias
            .SingleAsync(f => f.Id == membro.FamiliaId, cancellationToken);

        var response = await EmitirTokensAsync(usuario, familia, membro.Papel, cancellationToken, persistir: false);
        tokenAtual.Revogar(utcNow, tokenService.HashToken(response.RefreshToken));
        await dbContext.SaveChangesAsync(cancellationToken);

        return response;
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var tokenHash = tokenService.HashToken(refreshToken);
        var token = await dbContext.RefreshTokens
            .SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (token is not null && token.EstaAtivo(clock.UtcNow))
        {
            token.Revogar(clock.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<(MembroFamilia Membro, Familia Familia)> GarantirMembroFamiliaAsync(
        Usuario usuario,
        CancellationToken cancellationToken)
    {
        var membroExistente = await ObterMembroAtivoAsync(usuario, cancellationToken);
        if (membroExistente is not null)
        {
            var familiaExistente = await dbContext.Familias
                .SingleAsync(f => f.Id == membroExistente.FamiliaId, cancellationToken);
            return (membroExistente, familiaExistente);
        }

        var familiaPadraoId = identidadeOptions.Value.FamiliaPadraoId;
        if (familiaPadraoId.HasValue)
        {
            var familiaPadrao = await dbContext.Familias
                .Include(f => f.Membros)
                .SingleOrDefaultAsync(f => f.Id == familiaPadraoId.Value, cancellationToken);

            // A família padrão só absorve o primeiro usuário (dono do histórico pré-multi-tenant);
            // demais usuários entram apenas por convite.
            if (familiaPadrao is not null && familiaPadrao.Membros.Count == 0)
            {
                return (familiaPadrao.AdicionarMembro(usuario.Id, PapelFamilia.Administrador), familiaPadrao);
            }
        }

        var novaFamilia = Familia.Criar($"Família de {usuario.Nome}");
        dbContext.Familias.Add(novaFamilia);
        return (novaFamilia.AdicionarMembro(usuario.Id, PapelFamilia.Administrador), novaFamilia);
    }

    private async Task<MembroFamilia?> ObterMembroAtivoAsync(Usuario usuario, CancellationToken cancellationToken)
    {
        if (usuario.FamiliaAtivaId.HasValue)
        {
            var membroAtivo = await dbContext.MembrosFamilia
                .SingleOrDefaultAsync(
                    m => m.UsuarioId == usuario.Id && m.FamiliaId == usuario.FamiliaAtivaId.Value,
                    cancellationToken);

            if (membroAtivo is not null)
            {
                return membroAtivo;
            }
        }

        return await dbContext.MembrosFamilia
            .Where(m => m.UsuarioId == usuario.Id)
            .OrderBy(m => m.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<AuthTokenResponse> EmitirTokensAsync(
        Usuario usuario,
        Familia familia,
        PapelFamilia papel,
        CancellationToken cancellationToken,
        bool persistir = true)
    {
        var accessToken = tokenService.CreateAccessToken(usuario, familia.Id, papel);
        var refreshTokenValue = tokenService.GenerateOpaqueToken();

        dbContext.RefreshTokens.Add(RefreshToken.Criar(
            usuario.Id,
            tokenService.HashToken(refreshTokenValue),
            clock.UtcNow.Add(tokenService.RefreshTokenLifetime)));

        if (persistir)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new AuthTokenResponse(
            accessToken.AccessToken,
            accessToken.ExpiresAtUtc,
            refreshTokenValue,
            new UsuarioAutenticadoResponse(
                usuario.Id,
                usuario.Email,
                usuario.Nome,
                usuario.AvatarUrl,
                new FamiliaResumoResponse(familia.Id, familia.Nome, papel.ToString())));
    }
}
