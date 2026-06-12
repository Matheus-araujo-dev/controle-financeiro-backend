using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.Identidade;

public enum StatusConviteFamilia
{
    Pendente = 1,
    Aceito = 2,
    Revogado = 3,
    Expirado = 4
}

public sealed class ConviteFamilia : AuditableEntity
{
    private ConviteFamilia()
    {
    }

    public Guid FamiliaId { get; private set; }

    public string EmailConvidado { get; private set; } = string.Empty;

    public PapelFamilia Papel { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTime ExpiraEmUtc { get; private set; }

    public StatusConviteFamilia Status { get; private set; }

    public Guid? UsuarioAceiteId { get; private set; }

    public DateTime? AceitoEmUtc { get; private set; }

    public static ConviteFamilia Criar(
        Guid familiaId,
        string emailConvidado,
        PapelFamilia papel,
        string tokenHash,
        DateTime expiraEmUtc)
    {
        if (familiaId == Guid.Empty)
        {
            throw new ArgumentException("Família é obrigatória.", nameof(familiaId));
        }

        if (string.IsNullOrWhiteSpace(emailConvidado))
        {
            throw new ArgumentException("E-mail do convidado é obrigatório.", nameof(emailConvidado));
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException("Token é obrigatório.", nameof(tokenHash));
        }

        return new ConviteFamilia
        {
            FamiliaId = familiaId,
            EmailConvidado = emailConvidado.Trim().ToLowerInvariant(),
            Papel = papel,
            TokenHash = tokenHash,
            ExpiraEmUtc = expiraEmUtc,
            Status = StatusConviteFamilia.Pendente
        };
    }

    public bool EstaValido(DateTime utcNow) =>
        Status == StatusConviteFamilia.Pendente && ExpiraEmUtc > utcNow;

    public void Aceitar(Guid usuarioId, DateTime utcNow)
    {
        if (!EstaValido(utcNow))
        {
            throw new InvalidOperationException("Convite não está mais válido.");
        }

        Status = StatusConviteFamilia.Aceito;
        UsuarioAceiteId = usuarioId;
        AceitoEmUtc = utcNow;
    }

    public void Revogar()
    {
        if (Status == StatusConviteFamilia.Aceito)
        {
            throw new InvalidOperationException("Convite já aceito não pode ser revogado.");
        }

        Status = StatusConviteFamilia.Revogado;
    }

    public void MarcarExpirado() => Status = StatusConviteFamilia.Expirado;
}
