using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.Identidade;

public sealed class Familia : AuditableEntity
{
    private readonly List<MembroFamilia> _membros = [];

    private Familia()
    {
    }

    public string Nome { get; private set; } = string.Empty;

    public bool Ativa { get; private set; }

    public IReadOnlyCollection<MembroFamilia> Membros => _membros.AsReadOnly();

    public static Familia Criar(string nome)
    {
        var familia = new Familia { Ativa = true };
        familia.Renomear(nome);
        return familia;
    }

    public void Renomear(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new ArgumentException("Nome da família é obrigatório.", nameof(nome));
        }

        Nome = nome.Trim();
    }

    public MembroFamilia AdicionarMembro(Guid usuarioId, PapelFamilia papel)
    {
        if (_membros.Any(membro => membro.UsuarioId == usuarioId))
        {
            throw new InvalidOperationException("Usuário já é membro desta família.");
        }

        var membro = MembroFamilia.Criar(Id, usuarioId, papel);
        _membros.Add(membro);
        return membro;
    }
}
