using ControleFinanceiro.Contracts.Financeiro.Common;
using ControleFinanceiro.Contracts.Financeiro.ContasReceber;
using ControleFinanceiro.Domain.Financeiro;

namespace ControleFinanceiro.Application.Financeiro.ContasReceber;

/// <summary>Thin facade kept for controller stability; delegates to focused query and command services.</summary>
public sealed class ContaReceberAppService(
    IContaReceberQueryService queryService,
    IContaReceberCommandService commandService)
{
    public Task<ContaReceberListResponse> ListarAsync(ContaReceberListQueryRequest query, CancellationToken cancellationToken) =>
        queryService.ListarAsync(query, cancellationToken);

    public Task<ContaReceberDetalheResponse?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        queryService.ObterPorIdAsync(id, cancellationToken);

    public Task<ContaReceberDetalheResponse> CriarAsync(CriarContaReceberRequest request, CancellationToken cancellationToken) =>
        commandService.CriarAsync(request, cancellationToken);

    public Task<ContaReceberDetalheResponse?> AtualizarAsync(Guid id, AtualizarContaReceberRequest request, CancellationToken cancellationToken) =>
        commandService.AtualizarAsync(id, request, cancellationToken);

    public Task<ContaReceberDetalheResponse?> AlterarFuturasAsync(Guid id, AtualizarContaReceberRequest request, CancellationToken cancellationToken) =>
        commandService.AlterarFuturasAsync(id, request, cancellationToken);

    public Task<ContaReceberDetalheResponse?> GerarOcorrenciasAsync(Guid id, GerarOcorrenciasRecorrenciaRequest request, CancellationToken cancellationToken) =>
        commandService.GerarOcorrenciasAsync(id, request, cancellationToken);

    public Task<ContaReceberDetalheResponse?> PausarRecorrenciaAsync(Guid id, CancellationToken cancellationToken) =>
        commandService.PausarRecorrenciaAsync(id, cancellationToken);

    public Task<ContaReceberDetalheResponse?> EncerrarRecorrenciaAsync(Guid id, EncerrarRecorrenciaRequest request, CancellationToken cancellationToken) =>
        commandService.EncerrarRecorrenciaAsync(id, request, cancellationToken);

    public Task<ContaReceberDetalheResponse?> LiquidarAsync(Guid id, LiquidarContaReceberRequest request, CancellationToken cancellationToken) =>
        commandService.LiquidarAsync(id, request, cancellationToken);

    public Task<ContaReceberDetalheResponse?> EstornarAsync(Guid id, CancellationToken cancellationToken) =>
        commandService.EstornarAsync(id, cancellationToken);

    public Task<ContaReceberDetalheResponse?> CancelarAsync(Guid id, CancelarContaReceberRequest? request, CancellationToken cancellationToken) =>
        commandService.CancelarAsync(id, request, cancellationToken);
}
