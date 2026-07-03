using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ControleFinanceiro.Application.Financeiro.Status;

/// <summary>
/// Transiciona para "Pendente" as contas a pagar/receber com status "Futuro" cujo mês de
/// vencimento chegou. Rodada por um worker sem tenant, cobre todas as famílias em duas
/// instruções de UPDATE bulk, de forma idempotente.
/// </summary>
public sealed class TransicaoStatusFuturoService(
    IAppDbContext dbContext,
    ILogger<TransicaoStatusFuturoService> logger)
{
    public async Task<int> TransicionarFuturoParaPendenteAsync(CancellationToken cancellationToken)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var fimMesAtual = new DateOnly(hoje.Year, hoje.Month, DateTime.DaysInMonth(hoje.Year, hoje.Month));
        var agora = DateTime.UtcNow;

        var contasPagarAtualizadas = await dbContext.ContasPagar
            .Where(conta => conta.StatusContaId == StatusConta.FuturoId && conta.DataVencimento <= fimMesAtual)
            .ExecuteUpdateAsync(
                updates => updates
                    .SetProperty(conta => conta.StatusContaId, StatusConta.PendenteId)
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
                "Transição FUTURO→PENDENTE: {Pagar} conta(s) a pagar e {Receber} conta(s) a receber atualizadas.",
                contasPagarAtualizadas,
                contasReceberAtualizadas);
        }

        return total;
    }
}
