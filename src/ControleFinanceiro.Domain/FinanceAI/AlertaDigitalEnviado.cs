namespace ControleFinanceiro.Domain.FinanceAI;

public sealed class AlertaDigitalEnviado
{
    public const string CanalEmail = "Email";
    public const string CanalPush = "Push";
    public const string TipoVencimento = "Vencimento";
    public const string TipoLimiteCategoria = "LimiteCategoria";

    private AlertaDigitalEnviado() { }

    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    public string Canal { get; private set; } = null!;
    public string TipoAlerta { get; private set; } = null!;
    public string ChaveReferencia { get; private set; } = null!;
    public DateOnly DataEnvio { get; private set; }

    public static AlertaDigitalEnviado Registrar(
        Guid usuarioId,
        string canal,
        string tipoAlerta,
        string chaveReferencia,
        DateOnly dataEnvio) =>
        new()
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Canal = canal,
            TipoAlerta = tipoAlerta,
            ChaveReferencia = chaveReferencia,
            DataEnvio = dataEnvio
        };
}
