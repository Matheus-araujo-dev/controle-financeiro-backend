using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Contracts.Financeiro.ContasReceber;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Financeiro.ContasReceber;

public interface IContaReceberLiquidacaoService
{
    Task<ContaReceberDetalheResponse?> LiquidarAsync(Guid id, LiquidarContaReceberRequest request, CancellationToken cancellationToken);
    Task<ContaReceberDetalheResponse?> EstornarAsync(Guid id, CancellationToken cancellationToken);
    Task<ContaReceberDetalheResponse?> CancelarAsync(Guid id, CancelarContaReceberRequest? request, CancellationToken cancellationToken);
}

public sealed class ContaReceberLiquidacaoService(
    IAppDbContext dbContext,
    IContaReceberQueryService queryService,
    ContaReceberSharedHelper helper) : IContaReceberLiquidacaoService
{
    public async Task<ContaReceberDetalheResponse?> LiquidarAsync(
        Guid id, LiquidarContaReceberRequest request, CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasReceber.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (conta is null) return null;

        if (conta.StatusContaId == StatusConta.LiquidadaId)
            throw ValidationExceptionFactory.Create("Status", "Conta já está liquidada.");

        if (!await dbContext.ContasBancarias.AnyAsync(x => x.Id == request.ContaBancariaId, cancellationToken))
            throw ValidationExceptionFactory.Create("ContaBancariaId", "Conta bancária não encontrada.");

        if (request.FormaPagamentoId.HasValue &&
            await helper.LookupCache.GetFormaPagamentoByIdAsync(request.FormaPagamentoId.Value, cancellationToken) is null)
            throw ValidationExceptionFactory.Create("FormaPagamentoId", "Forma de pagamento não encontrada.");

        var saldoJaLiquidado = await helper.CalcularSaldoLiquidadoAsync(conta.Id, cancellationToken);
        var statusFinal = StatusConta.LiquidadaId;
        var valorMovimentacao = conta.ValorLiquido;

        var deveAtualizarValor = request.ValorLiquidacao > conta.ValorLiquido || request.AtualizarValorConta;
        var valorReferenciaConta = conta.ValorLiquido;

        if (deveAtualizarValor)
        {
            if (saldoJaLiquidado > 0)
                throw ValidationExceptionFactory.Create(
                    "ValorLiquidacao",
                    "Conta com liquidações parciais já registradas não pode ter o valor atualizado.");

            var novosRateios = await helper.RecalcularRateiosAsync(conta.Id, request.ValorLiquidacao, cancellationToken);
            conta.AtualizarValorLiquido(request.ValorLiquidacao, novosRateios);
            valorReferenciaConta = request.ValorLiquidacao;

            if (conta.RegraRecorrenciaId.HasValue && request.AtualizarRecorrencia)
                await helper.AtualizarTemplateRecorrenciaAsync(conta.RegraRecorrenciaId.Value, request.ValorLiquidacao, novosRateios, cancellationToken);
        }

        var saldoFinal = saldoJaLiquidado + request.ValorLiquidacao;

        if (request.CancelarValorRestante && saldoFinal < valorReferenciaConta)
        {
            var novosRateiosCancelamento = await helper.RecalcularRateiosAsync(conta.Id, saldoFinal, cancellationToken);
            conta.AtualizarValorLiquido(saldoFinal, novosRateiosCancelamento);
            if (conta.RegraRecorrenciaId.HasValue && request.AtualizarRecorrencia)
                await helper.AtualizarTemplateRecorrenciaAsync(conta.RegraRecorrenciaId.Value, saldoFinal, novosRateiosCancelamento, cancellationToken);
            statusFinal = StatusConta.LiquidadaId;
        }
        else
        {
            statusFinal = saldoFinal < valorReferenciaConta ? StatusConta.ParcialId : StatusConta.LiquidadaId;
        }

        valorMovimentacao = request.ValorLiquidacao;

        conta.Liquidar(request.DataLiquidacao, request.ContaBancariaId, statusFinal);
        dbContext.MovimentacoesFinanceiras.Add(
            MovimentacaoFinanceira.CriarLiquidacaoContaReceber(
                conta.Id, request.ContaBancariaId, request.DataLiquidacao,
                valorMovimentacao, StatusMovimentacao.EfetivadaId, conta.Descricao));

        await dbContext.SaveChangesAsync(cancellationToken);
        return await queryService.ObterPorIdAsync(conta.Id, cancellationToken);
    }

    public async Task<ContaReceberDetalheResponse?> EstornarAsync(Guid id, CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasReceber.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (conta is null) return null;

        try
        {
            conta.Estornar(StatusConta.PendenteId);
        }
        catch (InvalidOperationException exception)
        {
            throw ContaReceberSharedHelper.ConverterParaValidacao(exception);
        }

        var movimentos = await dbContext.MovimentacoesFinanceiras
            .Where(x =>
                x.ContaReceberId == conta.Id &&
                x.Natureza == NaturezaMovimentacao.Realizada &&
                x.StatusMovimentacaoId != StatusMovimentacao.CanceladaId)
            .ToListAsync(cancellationToken);

        foreach (var movimento in movimentos)
            movimento.Cancelar(StatusMovimentacao.CanceladaId);

        await dbContext.SaveChangesAsync(cancellationToken);
        return await queryService.ObterPorIdAsync(conta.Id, cancellationToken);
    }

    public async Task<ContaReceberDetalheResponse?> CancelarAsync(
        Guid id, CancelarContaReceberRequest? request, CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasReceber.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (conta is null) return null;

        if (conta.StatusContaId == StatusConta.ParcialId)
        {
            var saldoPago = await helper.CalcularSaldoLiquidadoAsync(conta.Id, cancellationToken);
            var novosRateios = await helper.RecalcularRateiosAsync(conta.Id, saldoPago, cancellationToken);
            conta.AtualizarValorLiquido(saldoPago, novosRateios);
            conta.Liquidar(conta.DataLiquidacao!.Value, conta.ContaBancariaId!.Value, StatusConta.LiquidadaId);
            await dbContext.SaveChangesAsync(cancellationToken);
            return await queryService.ObterPorIdAsync(conta.Id, cancellationToken);
        }

        try
        {
            conta.Cancelar(StatusConta.CanceladaId);
        }
        catch (InvalidOperationException exception)
        {
            throw ContaReceberSharedHelper.ConverterParaValidacao(exception);
        }

        if (conta.RegraRecorrenciaId.HasValue && request?.PausarRecorrenciaRelacionada == true)
        {
            var regra = await dbContext.RegrasRecorrencia
                .SingleOrDefaultAsync(x => x.Id == conta.RegraRecorrenciaId.Value, cancellationToken);
            if (regra is not null && regra.Ativa)
                regra.Pausar();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await queryService.ObterPorIdAsync(conta.Id, cancellationToken);
    }
}
