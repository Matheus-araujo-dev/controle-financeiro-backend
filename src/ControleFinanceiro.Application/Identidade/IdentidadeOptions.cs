namespace ControleFinanceiro.Application.Identidade;

public sealed class IdentidadeOptions
{
    public const string SectionName = "Identidade";

    /// <summary>
    /// Família usada no backfill dos dados pré-multi-tenant; o primeiro usuário a logar
    /// assume esta família como administrador para preservar o histórico existente.
    /// </summary>
    public Guid? FamiliaPadraoId { get; init; }

    public int ConviteExpiracaoHoras { get; init; } = 72;
}
