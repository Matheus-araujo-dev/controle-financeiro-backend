namespace ControleFinanceiro.Contracts.Financeiro.Planos;

public record CriarPlanoRequest(
    string Nome,
    string? Descricao,
    decimal ValorMensal,
    int NumParcelas,
    Guid ContaBancariaCaixaId,
    Guid? FormaPagamentoId = null,
    Guid? RecebedorId = null,
    Guid? ContaGerencialId = null);

public record AtualizarPlanoRequest(
    string Nome,
    string? Descricao,
    decimal ValorMensal,
    int NumParcelas,
    Guid? FormaPagamentoId = null,
    Guid? RecebedorId = null,
    Guid? ContaGerencialId = null);

public record RetirarDinheiroRequest(decimal Valor);

public record PlanoResumoResponse(
    Guid Id,
    string Nome,
    string? Descricao,
    decimal ValorMensal,
    int NumParcelas,
    Guid ContaBancariaCaixaId,
    string ContaBancariaNome,
    Guid? FormaPagamentoId,
    Guid? RecebedorId,
    Guid? ContaGerencialId,
    int ParcelasPagas,
    decimal TotalRetirado,
    decimal ValorTotal,
    decimal TotalAcumulado,
    bool Concluido,
    bool Cancelado,
    DateTime CreatedAtUtc);

public record PlanoListResponse(
    IReadOnlyList<PlanoResumoResponse> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);

public record PlanoListQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    bool? Cancelado = null,
    bool? Concluido = null,
    Guid? ContaBancariaCaixaId = null);
