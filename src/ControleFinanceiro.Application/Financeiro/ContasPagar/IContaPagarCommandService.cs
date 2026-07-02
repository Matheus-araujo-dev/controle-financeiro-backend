using ControleFinanceiro.Contracts.Financeiro.Common;
using ControleFinanceiro.Contracts.Financeiro.ContasPagar;

namespace ControleFinanceiro.Application.Financeiro.ContasPagar;

public interface IContaPagarCommandService
{
    Task<ContaPagarDetalheResponse> CriarAsync(CriarContaPagarRequest request, CancellationToken cancellationToken);
    Task<ContaPagarDetalheResponse?> AtualizarAsync(Guid id, AtualizarContaPagarRequest request, CancellationToken cancellationToken);
    Task<ContaPagarDetalheResponse?> AlterarFuturasAsync(Guid id, AtualizarContaPagarRequest request, CancellationToken cancellationToken);
    Task<ContaPagarDetalheResponse?> GerarOcorrenciasAsync(Guid id, GerarOcorrenciasRecorrenciaRequest request, CancellationToken cancellationToken);
    Task<ContaPagarDetalheResponse?> PausarRecorrenciaAsync(Guid id, CancellationToken cancellationToken);
    Task<ContaPagarDetalheResponse?> EncerrarRecorrenciaAsync(Guid id, EncerrarRecorrenciaRequest request, CancellationToken cancellationToken);
    Task<ContaPagarDetalheResponse?> LiquidarAsync(Guid id, LiquidarContaPagarRequest request, CancellationToken cancellationToken);
    Task<ContaPagarDetalheResponse?> EstornarAsync(Guid id, CancellationToken cancellationToken);
    Task<ContaPagarDetalheResponse?> CancelarAsync(Guid id, CancelarContaPagarRequest? request, CancellationToken cancellationToken);
}