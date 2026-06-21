using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.Financeiro;

public sealed class StatusMovimentacao : Entity
{
    public static readonly Guid PrevistaId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
    public static readonly Guid EfetivadaId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2");
    public static readonly Guid CanceladaId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa4");

    private StatusMovimentacao()
    {
    }

    private StatusMovimentacao(Guid id, string codigo, string nome)
    {
        Id = id;
        Codigo = codigo;
        Nome = nome;
    }

    public string Codigo { get; private set; } = string.Empty;

    public string Nome { get; private set; } = string.Empty;

    public static IReadOnlyCollection<StatusMovimentacao> Seeds() =>
    [
        new(PrevistaId, "PREVISTA", "Prevista"),
        new(EfetivadaId, "EFETIVADA", "Efetivada"),
        new(CanceladaId, "CANCELADA", "Cancelada")
    ];
}
