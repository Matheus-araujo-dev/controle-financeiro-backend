using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Contracts.Financeiro.Common;
using ControleFinanceiro.Contracts.Financeiro.ContasPagar;

namespace ControleFinanceiro.Application.Financeiro.ContasPagar;

public sealed class ContaPagarAppService(
    IContaPagarQueryService queryService,
    IContaPagarCommandService commandService) : IContaPagarAppService
{
    private readonly IContaPagarQueryService _queryService = queryService;
    private readonly IContaPagarCommandService _commandService = commandService;

    public Task<ContaPagarListResponse> ListarAsync(
        ContaPagarListQueryRequest query,
        CancellationToken cancellationToken)
        => _queryService.ListarAsync(query, cancellationToken);

    public Task<CursorPagedResult<ContaPagarResumoResponse>> ListarCursorAsync(
        ContaPagarCursorQueryRequest query,
        CancellationToken cancellationToken)
        => _queryService.ListarCursorAsync(query, cancellationToken);

    public Task<ContaPagarDetalheResponse?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
        => _queryService.ObterPorIdAsync(id, cancellationToken);

    public Task<ContaPagarDetalheResponse> CriarAsync(CriarContaPagarRequest request, CancellationToken cancellationToken)
        => _commandService.CriarAsync(request, cancellationToken);

    public Task<ContaPagarDetalheResponse?> AtualizarAsync(Guid id, AtualizarContaPagarRequest request, CancellationToken cancellationToken)
        => _commandService.AtualizarAsync(id, request, cancellationToken);

    public Task<ContaPagarDetalheResponse?> AlterarFuturasAsync(Guid id, AtualizarContaPagarRequest request, CancellationToken cancellationToken)
        => _commandService.AlterarFuturasAsync(id, request, cancellationToken);

    public Task<ContaPagarDetalheResponse?> GerarOcorrenciasAsync(Guid id, GerarOcorrenciasRecorrenciaRequest request, CancellationToken cancellationToken)
        => _commandService.GerarOcorrenciasAsync(id, request, cancellationToken);

    public Task<ContaPagarDetalheResponse?> PausarRecorrenciaAsync(Guid id, CancellationToken cancellationToken)
        => _commandService.PausarRecorrenciaAsync(id, cancellationToken);

    public Task<ContaPagarDetalheResponse?> EncerrarRecorrenciaAsync(Guid id, EncerrarRecorrenciaRequest request, CancellationToken cancellationToken)
        => _commandService.EncerrarRecorrenciaAsync(id, request, cancellationToken);

    public Task<ContaPagarDetalheResponse?> LiquidarAsync(Guid id, LiquidarContaPagarRequest request, CancellationToken cancellationToken)
        => _commandService.LiquidarAsync(id, request, cancellationToken);

    public Task<ContaPagarDetalheResponse?> EstornarAsync(Guid id, CancellationToken cancellationToken)
        => _commandService.EstornarAsync(id, cancellationToken);

    public Task<ContaPagarDetalheResponse?> CancelarAsync(Guid id, CancellationToken cancellationToken)
        => _commandService.CancelarAsync(id, cancellationToken);
}