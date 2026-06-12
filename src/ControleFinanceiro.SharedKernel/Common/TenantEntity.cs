namespace ControleFinanceiro.SharedKernel.Common;

public interface ITenantEntity
{
    Guid FamiliaId { get; }

    void AtribuirFamilia(Guid familiaId);
}

/// <summary>
/// Entidade pertencente a uma família (tenant). O AppDbContext estampa FamiliaId
/// na inserção a partir do usuário corrente e aplica filtro global de consulta.
/// </summary>
public abstract class TenantEntity : AuditableEntity, ITenantEntity
{
    public Guid FamiliaId { get; private set; }

    public void AtribuirFamilia(Guid familiaId)
    {
        if (familiaId == Guid.Empty)
        {
            throw new ArgumentException("Família é obrigatória.", nameof(familiaId));
        }

        if (FamiliaId != Guid.Empty && FamiliaId != familiaId)
        {
            throw new InvalidOperationException("A entidade já pertence a outra família.");
        }

        FamiliaId = familiaId;
    }
}
