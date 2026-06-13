using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.FinanceAI;

public sealed class WhatsappUsuario : TenantEntity
{
    private WhatsappUsuario() { }

    public Guid UsuarioId { get; private set; }

    /// <summary>Número normalizado sem +, apenas dígitos: "5531999998888"</summary>
    public string Telefone { get; private set; } = string.Empty;

    public bool Ativo { get; private set; }

    public DateTimeOffset? VerificadoEm { get; private set; }

    public static WhatsappUsuario Criar(Guid familiaId, Guid usuarioId, string telefone)
    {
        var wup = new WhatsappUsuario
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Telefone = NormalizarTelefone(telefone),
            Ativo = false
        };
        wup.AtribuirFamilia(familiaId);
        return wup;
    }

    public void Verificar(DateTimeOffset momento)
    {
        Ativo = true;
        VerificadoEm = momento;
    }

    public void AtualizarTelefone(string telefone, DateTimeOffset momento)
    {
        Telefone = NormalizarTelefone(telefone);
        Ativo = true;
        VerificadoEm = momento;
    }

    public void Desativar() => Ativo = false;

    public static string NormalizarTelefone(string telefone) =>
        new string(telefone.Where(char.IsDigit).ToArray());
}
