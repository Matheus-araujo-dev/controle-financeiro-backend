using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.Identidade;

public enum PapelFamilia
{
    Administrador = 1,
    Membro = 2,
    Visualizador = 3
}

public sealed class MembroFamilia : AuditableEntity
{
    private MembroFamilia()
    {
    }

    public Guid FamiliaId { get; private set; }

    public Guid UsuarioId { get; private set; }

    public PapelFamilia Papel { get; private set; }

    public Usuario? Usuario { get; private set; }

    public static MembroFamilia Criar(Guid familiaId, Guid usuarioId, PapelFamilia papel)
    {
        if (familiaId == Guid.Empty)
        {
            throw new ArgumentException("Família é obrigatória.", nameof(familiaId));
        }

        if (usuarioId == Guid.Empty)
        {
            throw new ArgumentException("Usuário é obrigatório.", nameof(usuarioId));
        }

        return new MembroFamilia
        {
            FamiliaId = familiaId,
            UsuarioId = usuarioId,
            Papel = papel
        };
    }

    public void AlterarPapel(PapelFamilia papel) => Papel = papel;
}
