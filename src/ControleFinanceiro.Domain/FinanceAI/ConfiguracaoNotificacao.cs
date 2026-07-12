using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.FinanceAI;

public sealed class ConfiguracaoNotificacao : TenantEntity
{
    private ConfiguracaoNotificacao() { }

    public Guid UsuarioId { get; private set; }

    public bool EmailAtivo { get; private set; }
    public string? EmailDestinatario { get; private set; }
    public bool EmailVencimento { get; private set; }
    public int EmailDiasAntecedencia { get; private set; } = 3;
    public bool EmailLimiteCategoria { get; private set; }

    public bool PushAtivo { get; private set; }
    public bool PushVencimento { get; private set; }
    public int PushDiasAntecedencia { get; private set; } = 1;
    public bool PushLimiteCategoria { get; private set; }

    public static ConfiguracaoNotificacao CriarPadrao(Guid familiaId, Guid usuarioId)
    {
        var config = new ConfiguracaoNotificacao
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            EmailAtivo = false,
            EmailVencimento = true,
            EmailDiasAntecedencia = 3,
            EmailLimiteCategoria = false,
            PushAtivo = false,
            PushVencimento = true,
            PushDiasAntecedencia = 1,
            PushLimiteCategoria = false
        };
        config.AtribuirFamilia(familiaId);
        return config;
    }

    public void Atualizar(
        bool emailAtivo,
        string? emailDestinatario,
        bool emailVencimento,
        int emailDiasAntecedencia,
        bool emailLimiteCategoria,
        bool pushAtivo,
        bool pushVencimento,
        int pushDiasAntecedencia,
        bool pushLimiteCategoria)
    {
        EmailAtivo = emailAtivo;
        EmailDestinatario = string.IsNullOrWhiteSpace(emailDestinatario) ? null : emailDestinatario.Trim();
        EmailVencimento = emailVencimento;
        EmailDiasAntecedencia = Math.Clamp(emailDiasAntecedencia, 1, 30);
        EmailLimiteCategoria = emailLimiteCategoria;
        PushAtivo = pushAtivo;
        PushVencimento = pushVencimento;
        PushDiasAntecedencia = Math.Clamp(pushDiasAntecedencia, 1, 30);
        PushLimiteCategoria = pushLimiteCategoria;
    }
}
