namespace ControleFinanceiro.Contracts.Alertas;

public sealed record ConfiguracaoNotificacaoResponse(
    bool EmailAtivo,
    string? EmailDestinatario,
    bool EmailVencimento,
    int EmailDiasAntecedencia,
    bool EmailLimiteCategoria,
    bool PushAtivo,
    bool PushVencimento,
    int PushDiasAntecedencia,
    bool PushLimiteCategoria);

public sealed record SalvarConfiguracaoNotificacaoRequest(
    bool EmailAtivo,
    string? EmailDestinatario,
    bool EmailVencimento,
    int EmailDiasAntecedencia,
    bool EmailLimiteCategoria,
    bool PushAtivo,
    bool PushVencimento,
    int PushDiasAntecedencia,
    bool PushLimiteCategoria);

public sealed record RegistrarPushSubscriptionRequest(
    string Endpoint,
    string P256dh,
    string Auth);

public sealed record PushSubscriptionResponse(
    Guid Id,
    string Endpoint,
    bool Ativo);

public sealed record VapidPublicKeyResponse(string PublicKey);
