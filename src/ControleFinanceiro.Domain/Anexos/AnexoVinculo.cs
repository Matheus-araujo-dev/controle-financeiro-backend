using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.Anexos;

public sealed class AnexoVinculo : TenantEntity
{
    private AnexoVinculo() { }

    public Guid AnexoId { get; private set; }
    public TipoEntidadeAnexo TipoEntidade { get; private set; }
    public Guid EntidadeId { get; private set; }

    public static AnexoVinculo Criar(Guid anexoId, TipoEntidadeAnexo tipoEntidade, Guid entidadeId)
    {
        if (anexoId == Guid.Empty) throw new ArgumentException("Anexo é obrigatório.", nameof(anexoId));
        if (entidadeId == Guid.Empty) throw new ArgumentException("Entidade vinculada é obrigatória.", nameof(entidadeId));

        return new AnexoVinculo { AnexoId = anexoId, TipoEntidade = tipoEntidade, EntidadeId = entidadeId };
    }
}
