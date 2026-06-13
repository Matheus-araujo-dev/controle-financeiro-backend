namespace ControleFinanceiro.Contracts.Agente;

public sealed record WhatsappAlertasResponse(
    bool ReceberVencimento,
    int DiasAntecedenciaVencimento,
    bool ReceberLimiteCategoria,
    bool ReceberLimiteResponsavel);

public sealed record WhatsappAlertasRequest(
    bool ReceberVencimento,
    int DiasAntecedenciaVencimento,
    bool ReceberLimiteCategoria,
    bool ReceberLimiteResponsavel);
