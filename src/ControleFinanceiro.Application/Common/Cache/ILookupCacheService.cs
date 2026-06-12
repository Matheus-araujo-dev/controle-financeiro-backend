using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;
using ControleFinanceiro.Domain.Cadastros.FormasPagamento;
using ControleFinanceiro.Domain.Financeiro;

namespace ControleFinanceiro.Application.Common.Cache;

public interface ILookupCacheService
{
    Task<IReadOnlyList<StatusConta>> GetAllStatusContaAsync(CancellationToken cancellationToken);
    Task<StatusConta?> GetStatusContaByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<StatusMovimentacao>> GetAllStatusMovimentacaoAsync(CancellationToken cancellationToken);
    Task<StatusMovimentacao?> GetStatusMovimentacaoByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<FormaPagamento>> GetAllFormaPagamentoAsync(CancellationToken cancellationToken);
    Task<FormaPagamento?> GetFormaPagamentoByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ContaGerencial>> GetAllContaGerencialAsync(CancellationToken cancellationToken);
    Task<ContaGerencial?> GetContaGerencialByIdAsync(Guid id, CancellationToken cancellationToken);

    ValueTask RefreshStatusContaAsync(CancellationToken cancellationToken);
    ValueTask RefreshStatusMovimentacaoAsync(CancellationToken cancellationToken);
    ValueTask RefreshFormaPagamentoAsync(CancellationToken cancellationToken);
    ValueTask RefreshContaGerencialAsync(CancellationToken cancellationToken);

    ValueTask RefreshAllAsync(CancellationToken cancellationToken);
}