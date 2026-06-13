using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.FinanceAI;

public sealed class WhatsappConfigAlerta : TenantEntity
{
    private WhatsappConfigAlerta() { }

    public Guid UsuarioId { get; private set; }

    public bool ReceberVencimento { get; private set; }

    public int DiasAntecedenciaVencimento { get; private set; } = 3;

    public bool ReceberLimiteCategoria { get; private set; }

    public bool ReceberLimiteResponsavel { get; private set; }

    public static WhatsappConfigAlerta CriarPadrao(Guid familiaId, Guid usuarioId)
    {
        var config = new WhatsappConfigAlerta
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            ReceberVencimento = true,
            DiasAntecedenciaVencimento = 3,
            ReceberLimiteCategoria = false,
            ReceberLimiteResponsavel = false
        };
        config.AtribuirFamilia(familiaId);
        return config;
    }

    public void Atualizar(
        bool receberVencimento,
        int diasAntecedenciaVencimento,
        bool receberLimiteCategoria,
        bool receberLimiteResponsavel)
    {
        ReceberVencimento = receberVencimento;
        DiasAntecedenciaVencimento = Math.Clamp(diasAntecedenciaVencimento, 1, 30);
        ReceberLimiteCategoria = receberLimiteCategoria;
        ReceberLimiteResponsavel = receberLimiteResponsavel;
    }
}
