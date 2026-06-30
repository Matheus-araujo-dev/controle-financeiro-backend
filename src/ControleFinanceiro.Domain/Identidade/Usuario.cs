using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.Identidade;

public sealed class Usuario : AuditableEntity
{
    private Usuario()
    {
    }

    public string GoogleSubject { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string Nome { get; private set; } = string.Empty;

    public string? AvatarUrl { get; private set; }

    public Guid? FamiliaAtivaId { get; private set; }

    public bool Ativo { get; private set; }

    public static Usuario Criar(string googleSubject, string email, string nome, string? avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(googleSubject))
        {
            throw new ArgumentException("GoogleSubject é obrigatório.", nameof(googleSubject));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email é obrigatório.", nameof(email));
        }

        var usuario = new Usuario
        {
            GoogleSubject = googleSubject.Trim(),
            Ativo = true
        };

        usuario.AtualizarPerfil(email, nome, avatarUrl);
        return usuario;
    }

    public void AtualizarPerfil(string email, string nome, string? avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email é obrigatório.", nameof(email));
        }

        Email = email.Trim();
        Nome = string.IsNullOrWhiteSpace(nome) ? Email : nome.Trim();
        AvatarUrl = string.IsNullOrWhiteSpace(avatarUrl) ? null : avatarUrl.Trim();
    }

    public void DefinirFamiliaAtiva(Guid familiaId)
    {
        if (familiaId == Guid.Empty)
        {
            throw new ArgumentException("Família é obrigatória.", nameof(familiaId));
        }

        FamiliaAtivaId = familiaId;
    }

    public void Desativar() => Ativo = false;

    /// <summary>
    /// Anonimiza dados pessoais identificáveis conforme Art. 18 da LGPD (direito ao esquecimento).
    /// Preserva o registro para obrigações legais de auditoria mas remove PII.
    /// </summary>
    public void AnonimizarDados(string emailAnonimizado)
    {
        Email = emailAnonimizado;
        Nome = "Usuário Removido";
        AvatarUrl = null;
        GoogleSubject = string.Empty;
        Ativo = false;
    }
}
