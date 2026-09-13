using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ControleFinanceiro.Application.Financeiro.Status;

/// <summary>
/// Promove contas futuras do mês: cartão para "Em fatura" e demais para "Pendente".
/// Mantém os registros e os filtros de workspace, com atualizações idempotentes.
/// </summary>
public sealed class TransicaoStatusFuturoService(
    IAppDbContext dbContext,
    ILogger<TransicaoStatusFuturoService> logger)
{
    public async Task<int> TransicionarFuturoParaPendenteAsync(CancellationToken cancellationToken, DateOnly? dataReferencia = null)
    {
        var hoje = dataReferencia ?? DateOnly.FromDateTime(DateTime.Today);
        var fimMesAtual = new DateOnly(hoje.Year, hoje.Month, DateTime.DaysInMonth(hoje.Year, hoje.Month));
        var agora = DateTime.UtcNow;

        var contasPagarAtualizadas = await dbContext.ContasPagar
            .Where(conta => conta.StatusContaId == StatusConta.FuturoId && conta.DataVencimento <= fimMesAtual)
            .ExecuteUpdateAsync(
                updates => updates
                    .SetProperty(conta => conta.StatusContaId, conta => conta.CartaoId.HasValue ? StatusConta.EmFaturaId : StatusConta.PendenteId)
                    .SetProperty(conta => conta.UpdatedAtUtc, agora),
                cancellationToken);

        var contasReceberAtualizadas = await dbContext.ContasReceber
            .Where(conta => conta.StatusContaId == StatusConta.FuturoId && conta.DataVencimento <= fimMesAtual)
            .ExecuteUpdateAsync(
                updates => updates
                    .SetProperty(conta => conta.StatusContaId, StatusConta.PendenteId)
                    .SetProperty(conta => conta.UpdatedAtUtc, agora),
                cancellationToken);

        var total = contasPagarAtualizadas + contasReceberAtualizadas;
        if (total > 0)
        {
            logger.LogInformation(
                "Transição das contas FUTURO para o status do mês: {Pagar} conta(s) a pagar e {Receber} conta(s) a receber atualizadas.",
                contasPagarAtualizadas,
                contasReceberAtualizadas);
        }

        return total;
    }
}
