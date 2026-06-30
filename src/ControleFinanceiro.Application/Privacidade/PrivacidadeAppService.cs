using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Privacidade;

/// <summary>
/// Implementa o direito ao esquecimento conforme Art. 18 da LGPD.
/// Anonimiza dados pessoais do usuário sem remover registros financeiros (obrigação legal de guarda).
/// </summary>
public sealed class PrivacidadeAppService(IAppDbContext dbContext, ICurrentUser currentUser, IClock clock)
{
    public async Task SolicitarEsquecimentoAsync(CancellationToken cancellationToken)
    {
        var usuarioIdStr = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Usuário não autenticado.");

        if (!Guid.TryParse(usuarioIdStr, out var usuarioId))
            throw new UnauthorizedAccessException("UserId inválido.");

        var usuario = await dbContext.Usuarios
            .SingleOrDefaultAsync(u => u.Id == usuarioId, cancellationToken)
            ?? throw new InvalidOperationException("Usuário não encontrado.");

        // Anonimizar dados pessoais identificáveis (PII) preservando o vínculo para auditoria.
        var emailAnonimizado = $"usuario-removido-{usuarioId:N}@anonimizado.local";
        usuario.AnonimizarDados(emailAnonimizado);

        // Revogar todos os refresh tokens ativos.
        var utcNow = clock.UtcNow;
        var tokens = await dbContext.RefreshTokens
            .Where(t => t.UsuarioId == usuarioId && t.RevogadoEmUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.Revogar(utcNow);
        }

        // Remover perfil WhatsApp, se existir.
        var whatsapp = await dbContext.WhatsappUsuarios
            .Where(w => w.UsuarioId == usuarioId)
            .ToListAsync(cancellationToken);

        dbContext.WhatsappUsuarios.RemoveRange(whatsapp);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
