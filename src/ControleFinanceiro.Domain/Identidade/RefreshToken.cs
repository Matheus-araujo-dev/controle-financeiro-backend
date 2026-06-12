using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.Identidade;

public sealed class RefreshToken : AuditableEntity
{
    private RefreshToken()
    {
    }

    public Guid UsuarioId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTime ExpiraEmUtc { get; private set; }

    public DateTime? RevogadoEmUtc { get; private set; }

    public string? SubstituidoPorTokenHash { get; private set; }

    public static RefreshToken Criar(Guid usuarioId, string tokenHash, DateTime expiraEmUtc)
    {
        if (usuarioId == Guid.Empty)
        {
            throw new ArgumentException("Usuário é obrigatório.", nameof(usuarioId));
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException("Token é obrigatório.", nameof(tokenHash));
        }

        return new RefreshToken
        {
            UsuarioId = usuarioId,
            TokenHash = tokenHash,
            ExpiraEmUtc = expiraEmUtc
        };
    }

    public bool EstaAtivo(DateTime utcNow) =>
        RevogadoEmUtc is null && ExpiraEmUtc > utcNow;

    public void Revogar(DateTime utcNow, string? substituidoPorTokenHash = null)
    {
        RevogadoEmUtc = utcNow;
        SubstituidoPorTokenHash = substituidoPorTokenHash;
    }
}
