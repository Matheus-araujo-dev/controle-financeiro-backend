using ControleFinanceiro.Contracts.Financeiro.Common;
using ControleFinanceiro.Contracts.Financeiro.ContasPagar;

namespace ControleFinanceiro.Application.Financeiro.ContasPagar;

/// <summary>
/// Thin facade implementing IContaPagarCommandService by delegating to three focused services.
/// The controller depends on this facade so its signature remains stable.
/// </summary>
public sealed class ContaPagarCommandService(
    IContaPagarCriacaoService criacao,
    IContaPagarRecorrenciaService recorrencia,
    IContaPagarLiquidacaoService liquidacao) : IContaPagarCommandService
{
    public Task<ContaPagarDetalheResponse> CriarAsync(CriarContaPagarRequest request, CancellationToken cancellationToken) =>
        criacao.CriarAsync(request, cancellationToken);

    public Task<ContaPagarDetalheResponse?> AtualizarAsync(Guid id, AtualizarContaPagarRequest request, CancellationToken cancellationToken) =>
        recorrencia.AtualizarAsync(id, request, cancellationToken);

    public Task<ContaPagarDetalheResponse?> AlterarFuturasAsync(Guid id, AtualizarContaPagarRequest request, CancellationToken cancellationToken) =>
        recorrencia.AlterarFuturasAsync(id, request, cancellationToken);

    public Task<ContaPagarDetalheResponse?> GerarOcorrenciasAsync(Guid id, GerarOcorrenciasRecorrenciaRequest request, CancellationToken cancellationToken) =>
        recorrencia.GerarOcorrenciasAsync(id, request, cancellationToken);

    public Task<ContaPagarDetalheResponse?> PausarRecorrenciaAsync(Guid id, CancellationToken cancellationToken) =>
        recorrencia.PausarRecorrenciaAsync(id, cancellationToken);

    public Task<ContaPagarDetalheResponse?> EncerrarRecorrenciaAsync(Guid id, EncerrarRecorrenciaRequest request, CancellationToken cancellationToken) =>
        recorrencia.EncerrarRecorrenciaAsync(id, request, cancellationToken);

    public Task<ContaPagarDetalheResponse?> LiquidarAsync(Guid id, LiquidarContaPagarRequest request, CancellationToken cancellationToken) =>
        liquidacao.LiquidarAsync(id, request, cancellationToken);

    public Task<ContaPagarDetalheResponse?> EstornarAsync(Guid id, CancellationToken cancellationToken) =>
        liquidacao.EstornarAsync(id, cancellationToken);

    public Task<ContaPagarDetalheResponse?> CancelarAsync(Guid id, CancelarContaPagarRequest? request, CancellationToken cancellationToken) =>
        liquidacao.CancelarAsync(id, request, cancellationToken);
}
