using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.Financeiro;

public sealed class StatusConta : Entity
{
    public static readonly Guid PendenteId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid LiquidadaId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid VencidaId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid CanceladaId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid ParcialId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    public static readonly Guid EmFaturaId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private StatusConta()
    {
    }

    private StatusConta(Guid id, string codigo, string nome)
    {
        Id = id;
        Codigo = codigo;
        Nome = nome;
    }

    public string Codigo { get; private set; } = string.Empty;

    public string Nome { get; private set; } = string.Empty;

    public static IReadOnlyCollection<StatusConta> Seeds() =>
    [
        new(PendenteId, "PENDENTE", "Pendente"),
        new(LiquidadaId, "LIQUIDADA", "Liquidada"),
        new(VencidaId, "VENCIDA", "Vencida"),
        new(CanceladaId, "CANCELADA", "Cancelada"),
        new(ParcialId, "PARCIAL", "Parcial"),
        new(EmFaturaId, "EM_FATURA", "Em fatura")
    ];
}
