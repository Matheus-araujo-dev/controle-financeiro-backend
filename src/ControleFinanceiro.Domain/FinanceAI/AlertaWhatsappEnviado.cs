namespace ControleFinanceiro.Domain.FinanceAI;

public sealed class AlertaWhatsappEnviado
{
    public const string TipoVencimento = "vencimento";
    public const string TipoLimiteCategoria = "limite_categoria";

    private AlertaWhatsappEnviado() { }

    public Guid Id { get; private set; }
    public string Telefone { get; private set; } = string.Empty;
    public string TipoAlerta { get; private set; } = string.Empty;
    public string ChaveReferencia { get; private set; } = string.Empty;
    public DateOnly DataEnvio { get; private set; }

    public static AlertaWhatsappEnviado Registrar(
        string telefone, string tipoAlerta, string chaveReferencia, DateOnly dataEnvio) =>
        new()
        {
            Id = Guid.NewGuid(),
            Telefone = telefone,
            TipoAlerta = tipoAlerta,
            ChaveReferencia = chaveReferencia,
            DataEnvio = dataEnvio
        };
}
