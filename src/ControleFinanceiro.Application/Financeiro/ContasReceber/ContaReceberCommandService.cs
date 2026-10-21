using ControleFinanceiro.Contracts.Financeiro.Common;
using ControleFinanceiro.Contracts.Financeiro.ContasReceber;

namespace ControleFinanceiro.Application.Financeiro.ContasReceber;

public interface IContaReceberCommandService
{
    Task<ContaReceberDetalheResponse> CriarAsync(CriarContaReceberRequest request, CancellationToken cancellationToken);
    Task<ContaReceberDetalheResponse?> AtualizarAsync(Guid id, AtualizarContaReceberRequest request, CancellationToken cancellationToken);
    Task<ContaReceberDetalheResponse?> AlterarFuturasAsync(Guid id, AtualizarContaReceberRequest request, CancellationToken cancellationToken);
    Task<ContaReceberDetalheResponse?> GerarOcorrenciasAsync(Guid id, GerarOcorrenciasRecorrenciaRequest request, CancellationToken cancellationToken);
    Task<ContaReceberDetalheResponse?> PausarRecorrenciaAsync(Guid id, CancellationToken cancellationToken);
    Task<ContaReceberDetalheResponse?> EncerrarRecorrenciaAsync(Guid id, EncerrarRecorrenciaRequest request, CancellationToken cancellationToken);
    Task<ContaReceberDetalheResponse?> LiquidarAsync(Guid id, LiquidarContaReceberRequest request, CancellationToken cancellationToken);
    Task<ContaReceberDetalheResponse?> EstornarAsync(Guid id, CancellationToken cancellationToken);
    Task<ContaReceberDetalheResponse?> CancelarAsync(Guid id, CancelarContaReceberRequest? request, CancellationToken cancellationToken);
}

/// <summary>Thin facade implementing IContaReceberCommandService by delegating to three focused services.</summary>
public sealed class ContaReceberCommandService(
    IContaReceberCriacaoService criacao,
    IContaReceberRecorrenciaService recorrencia,
    IContaReceberLiquidacaoService liquidacao) : IContaReceberCommandService
{
    public Task<ContaReceberDetalheResponse> CriarAsync(CriarContaReceberRequest request, CancellationToken cancellationToken) =>
        criacao.CriarAsync(request, cancellationToken);

    public Task<ContaReceberDetalheResponse?> AtualizarAsync(Guid id, AtualizarContaReceberRequest request, CancellationToken cancellationToken) =>
        recorrencia.AtualizarAsync(id, request, cancellationToken);

    public Task<ContaReceberDetalheResponse?> AlterarFuturasAsync(Guid id, AtualizarContaReceberRequest request, CancellationToken cancellationToken) =>
        recorrencia.AlterarFuturasAsync(id, request, cancellationToken);

    public Task<ContaReceberDetalheResponse?> GerarOcorrenciasAsync(Guid id, GerarOcorrenciasRecorrenciaRequest request, CancellationToken cancellationToken) =>
        recorrencia.GerarOcorrenciasAsync(id, request, cancellationToken);

    public Task<ContaReceberDetalheResponse?> PausarRecorrenciaAsync(Guid id, CancellationToken cancellationToken) =>
        recorrencia.PausarRecorrenciaAsync(id, cancellationToken);

    public Task<ContaReceberDetalheResponse?> EncerrarRecorrenciaAsync(Guid id, EncerrarRecorrenciaRequest request, CancellationToken cancellationToken) =>
        recorrencia.EncerrarRecorrenciaAsync(id, request, cancellationToken);

    public Task<ContaReceberDetalheResponse?> LiquidarAsync(Guid id, LiquidarContaReceberRequest request, CancellationToken cancellationToken) =>
        liquidacao.LiquidarAsync(id, request, cancellationToken);

    public Task<ContaReceberDetalheResponse?> EstornarAsync(Guid id, CancellationToken cancellationToken) =>
        liquidacao.EstornarAsync(id, cancellationToken);

    public Task<ContaReceberDetalheResponse?> CancelarAsync(Guid id, CancelarContaReceberRequest? request, CancellationToken cancellationToken) =>
        liquidacao.CancelarAsync(id, request, cancellationToken);
}
