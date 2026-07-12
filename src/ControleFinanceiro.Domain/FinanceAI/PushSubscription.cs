using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.FinanceAI;

public sealed class PushSubscription : TenantEntity
{
    private PushSubscription() { }

    public Guid UsuarioId { get; private set; }
    public string Endpoint { get; private set; } = null!;
    public string P256dh { get; private set; } = null!;
    public string Auth { get; private set; } = null!;
    public bool Ativo { get; private set; } = true;

    public static PushSubscription Registrar(
        Guid familiaId,
        Guid usuarioId,
        string endpoint,
        string p256dh,
        string auth)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new ArgumentException("Endpoint é obrigatório.", nameof(endpoint));
        if (string.IsNullOrWhiteSpace(p256dh))
            throw new ArgumentException("Chave pública p256dh é obrigatória.", nameof(p256dh));
        if (string.IsNullOrWhiteSpace(auth))
            throw new ArgumentException("Auth é obrigatório.", nameof(auth));

        var sub = new PushSubscription
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Endpoint = endpoint.Trim(),
            P256dh = p256dh.Trim(),
            Auth = auth.Trim(),
            Ativo = true
        };
        sub.AtribuirFamilia(familiaId);
        return sub;
    }

    public void Desativar() => Ativo = false;
}
