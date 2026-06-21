using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ControleFinanceiro.Application.Financeiro.Status;

/// <summary>
/// Transiciona para "Vencida" as contas a pagar/receber que ainda estão "Pendente" mas cuja
/// data de vencimento já passou. É a rotina diária de manutenção de status — quando rodada por
/// um worker (sem tenant), o filtro global de família fica desativado e a varredura cobre todas
/// as famílias em uma única instrução por tabela.
/// </summary>
public sealed class AtualizacaoStatusContasService(
    IAppDbContext dbContext,
    ILogger<AtualizacaoStatusContasService> logger)
{
    public async Task<int> MarcarContasVencidasAsync(CancellationToken cancellationToken)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var agora = DateTime.UtcNow;

        // Apenas PENDENTE → VENCIDA: PARCIAL preserva o histórico de baixa parcial e cartões
        // ficam em EM_FATURA (liquidados em massa no pagamento da fatura), então ambos são ignorados.
        var contasPagarAtualizadas = await dbContext.ContasPagar
            .Where(conta => conta.StatusContaId == StatusConta.PendenteId && conta.DataVencimento < hoje)
            .ExecuteUpdateAsync(
                updates => updates
                    .SetProperty(conta => conta.StatusContaId, StatusConta.VencidaId)
                    .SetProperty(conta => conta.UpdatedAtUtc, agora),
                cancellationToken);

        var contasReceberAtualizadas = await dbContext.ContasReceber
            .Where(conta => conta.StatusContaId == StatusConta.PendenteId && conta.DataVencimento < hoje)
            .ExecuteUpdateAsync(
                updates => updates
                    .SetProperty(conta => conta.StatusContaId, StatusConta.VencidaId)
                    .SetProperty(conta => conta.UpdatedAtUtc, agora),
                cancellationToken);

        var total = contasPagarAtualizadas + contasReceberAtualizadas;
        if (total > 0)
        {
            logger.LogInformation(
                "Atualização de status: {Pagar} conta(s) a pagar e {Receber} conta(s) a receber marcadas como vencidas.",
                contasPagarAtualizadas,
                contasReceberAtualizadas);
        }

        return total;
    }
}
