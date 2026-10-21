using ControleFinanceiro.Contracts.Dashboard;

namespace ControleFinanceiro.Application.Dashboard;

/// <summary>
/// Thin facade that delegates each concern to a focused service.
/// The controller depends on this facade so its signature remains stable while
/// internal implementation is split across focused services.
/// </summary>
public sealed class DashboardAppService(
    IDashboardResumoService resumo,
    IDashboardFluxoCaixaService fluxoCaixa,
    IDashboardContasGerenciaisService contasGerenciais,
    IDashboardCentralPrevisaoService centralPrevisao,
    IDashboardResponsavelService responsavel,
    IDashboardComparativoMensalService comparativoMensal)
{
    public Task<DashboardResumoResponse> ObterResumoAsync(DashboardResumoQueryRequest query, CancellationToken cancellationToken) =>
        resumo.ObterResumoAsync(query, cancellationToken);

    public Task<DashboardFluxoCaixaResponse> ObterFluxoCaixaAsync(DashboardFluxoCaixaQueryRequest query, CancellationToken cancellationToken) =>
        fluxoCaixa.ObterFluxoCaixaAsync(query, cancellationToken);

    public Task<DashboardContaGerencialResumoResponse> ObterContasGerenciaisResumoAsync(DashboardContaGerencialResumoQueryRequest query, CancellationToken cancellationToken) =>
        contasGerenciais.ObterContasGerenciaisResumoAsync(query, cancellationToken);

    public Task<DashboardContaGerencialSerieResponse> ObterContasGerenciaisSerieAsync(DashboardContaGerencialSerieQueryRequest query, CancellationToken cancellationToken) =>
        contasGerenciais.ObterContasGerenciaisSerieAsync(query, cancellationToken);

    public Task<DashboardContaGerencialLancamentosResponse> ObterContaGerencialLancamentosAsync(DashboardContaGerencialLancamentosQueryRequest query, CancellationToken cancellationToken) =>
        contasGerenciais.ObterContaGerencialLancamentosAsync(query, cancellationToken);

    public Task<DashboardCentralPrevisaoResumoResponse> ObterCentralPrevisaoResumoAsync(DashboardCentralPrevisaoQueryRequest query, CancellationToken cancellationToken) =>
        centralPrevisao.ObterCentralPrevisaoResumoAsync(query, cancellationToken);

    public Task<DashboardCentralPrevisaoItensResponse> ObterCentralPrevisaoItensAsync(DashboardCentralPrevisaoItensQueryRequest query, CancellationToken cancellationToken) =>
        centralPrevisao.ObterCentralPrevisaoItensAsync(query, cancellationToken);

    public Task<DashboardResponsavelResumoResponse> ObterResumoPorResponsavelAsync(DashboardResponsavelQueryRequest query, CancellationToken cancellationToken) =>
        responsavel.ObterResumoPorResponsavelAsync(query, cancellationToken);

    public Task<DashboardComparativoMensalResponse> ObterComparativoMensalAsync(DashboardComparativoMensalQueryRequest query, CancellationToken cancellationToken) =>
        comparativoMensal.ObterComparativoMensalAsync(query, cancellationToken);
}
