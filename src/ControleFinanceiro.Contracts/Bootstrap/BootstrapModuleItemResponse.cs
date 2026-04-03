namespace ControleFinanceiro.Contracts.Bootstrap;

public sealed record BootstrapModuleItemResponse(
    string Code,
    string Name,
    string Route,
    int Phase);
