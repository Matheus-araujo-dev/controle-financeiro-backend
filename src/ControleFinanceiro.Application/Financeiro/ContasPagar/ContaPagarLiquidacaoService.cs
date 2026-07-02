using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Contracts.Financeiro.ContasPagar;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Financeiro.ContasPagar;

public interface IContaPagarLiquidacaoService
{
    Task<ContaPagarDetalheResponse?> LiquidarAsync(Guid id, LiquidarContaPagarRequest request, CancellationToken cancellationToken);
    Task<ContaPagarDetalheResponse?> EstornarAsync(Guid id, CancellationToken cancellationToken);
    Task<ContaPagarDetalheResponse?> CancelarAsync(Guid id, CancelarContaPagarRequest? request, CancellationToken cancellationToken);
}

public sealed class ContaPagarLiquidacaoService(
    IAppDbContext dbContext,
    IContaPagarQueryService queryService,
    ContaPagarSharedHelper helper) : IContaPagarLiquidacaoService
{
    public async Task<ContaPagarDetalheResponse?> LiquidarAsync(Guid id, LiquidarContaPagarRequest request, CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasPagar.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (conta is null) return null;

        if (conta.StatusContaId == StatusConta.LiquidadaId)
            throw helper.CriarErroValidacao("Status", "Conta já está liquidada.");

        if (!await dbContext.ContasBancarias.AnyAsync(x => x.Id == request.ContaBancariaId, cancellationToken))
            throw helper.CriarErroValidacao("ContaBancariaId", "Conta bancária não encontrada.");

        if (request.FormaPagamentoId.HasValue &&
            await helper.LookupCache.GetFormaPagamentoByIdAsync(request.FormaPagamentoId.Value, cancellationToken) is null)
            throw helper.CriarErroValidacao("FormaPagamentoId", "Forma de pagamento não encontrada.");

        var formaPagamento = await helper.LookupCache.GetFormaPagamentoByIdAsync(conta.FormaPagamentoId, cancellationToken)
            ?? throw new InvalidOperationException("FormaPagamento not found");

        if ((formaPagamento.EhCartao || conta.CartaoId.HasValue) && !conta.FaturaCartaoId.HasValue)
            throw helper.CriarErroValidacao("CartaoId", "Compras em cartão devem ser liquidadas pela fatura.");

        var saldoJaLiquidado = await helper.CalcularSaldoLiquidadoAsync(conta.Id, cancellationToken);
        var statusFinal = StatusConta.LiquidadaId;
        var valorMovimentacao = conta.ValorLiquido;

        if (!formaPagamento.EhCartao && !conta.CartaoId.HasValue)
        {
            var deveAtualizarValor = request.ValorLiquidacao > conta.ValorLiquido || request.AtualizarValorConta;
            var valorReferenciaConta = conta.ValorLiquido;

            if (deveAtualizarValor)
            {
                if (saldoJaLiquidado > 0)
                    throw helper.CriarErroValidacao("ValorLiquidacao", "Conta com liquidacoes parciais ja registradas nao pode ter o valor atualizado.");

                var novosRateios = await helper.RecalcularRateiosAsync(conta.Id, request.ValorLiquidacao, cancellationToken);
                conta.AtualizarValorLiquido(request.ValorLiquidacao, novosRateios);
                valorReferenciaConta = request.ValorLiquidacao;

                if (conta.RegraRecorrenciaId.HasValue)
                    await helper.AtualizarTemplateRecorrenciaAsync(conta.RegraRecorrenciaId.Value, request.ValorLiquidacao, novosRateios, cancellationToken);
            }

            var saldoFinal = saldoJaLiquidado + request.ValorLiquidacao;
            statusFinal = saldoFinal < valorReferenciaConta ? StatusConta.ParcialId : StatusConta.LiquidadaId;
            valorMovimentacao = request.ValorLiquidacao;
        }

        conta.Liquidar(request.DataLiquidacao, request.ContaBancariaId, statusFinal);

        if (conta.FaturaCartaoId.HasValue)
        {
            var fatura = await dbContext.FaturasCartao.SingleAsync(x => x.Id == conta.FaturaCartaoId.Value, cancellationToken);
            if (fatura.Status != StatusFaturaCartao.Paga)
                fatura.Pagar(request.DataLiquidacao, request.ContaBancariaId, conta.Observacao);

            var cartao = await dbContext.Cartoes.AsNoTracking().SingleAsync(x => x.Id == fatura.CartaoId, cancellationToken);
            var itensDaFatura = await dbContext.ContasPagar
                .Where(x => x.CartaoId == fatura.CartaoId && x.StatusContaId != StatusConta.CanceladaId)
                .ToListAsync(cancellationToken);

            foreach (var itemFatura in itensDaFatura
                .Where(x => FaturaCartaoCompetencia.Calcular(x.DataEmissao, cartao.DiaFechamentoFatura, cartao.DiaVencimentoFatura).Competencia == fatura.Competencia)
                .Where(x => x.StatusContaId != StatusConta.LiquidadaId))
            {
                itemFatura.Liquidar(request.DataLiquidacao, request.ContaBancariaId, StatusConta.LiquidadaId);
            }
        }

        dbContext.MovimentacoesFinanceiras.Add(
            MovimentacaoFinanceira.CriarLiquidacaoContaPagar(
                conta.Id, request.ContaBancariaId, request.DataLiquidacao, valorMovimentacao,
                StatusMovimentacao.EfetivadaId, conta.Descricao, conta.FaturaCartaoId));

        await dbContext.SaveChangesAsync(cancellationToken);
        return await queryService.ObterPorIdAsync(conta.Id, cancellationToken);
    }

    public async Task<ContaPagarDetalheResponse?> EstornarAsync(Guid id, CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasPagar.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (conta is null) return null;

        try { conta.Estornar(StatusConta.PendenteId); }
        catch (InvalidOperationException ex) { throw helper.ConverterParaValidacao(ex); }

        var movimentos = await dbContext.MovimentacoesFinanceiras
            .Where(x => x.ContaPagarId == conta.Id &&
                        x.Natureza == NaturezaMovimentacao.Realizada &&
                        x.StatusMovimentacaoId != StatusMovimentacao.CanceladaId)
            .ToListAsync(cancellationToken);
        foreach (var movimento in movimentos) movimento.Cancelar(StatusMovimentacao.CanceladaId);

        if (conta.FaturaCartaoId.HasValue)
        {
            var fatura = await dbContext.FaturasCartao.SingleAsync(x => x.Id == conta.FaturaCartaoId.Value, cancellationToken);
            if (fatura.Status == StatusFaturaCartao.Paga) fatura.ReabrirPagamento();

            var cartao = await dbContext.Cartoes.AsNoTracking().SingleAsync(x => x.Id == fatura.CartaoId, cancellationToken);
            var itensDaFatura = await dbContext.ContasPagar
                .Where(x => x.CartaoId == fatura.CartaoId && x.StatusContaId != StatusConta.CanceladaId)
                .ToListAsync(cancellationToken);

            foreach (var itemFatura in itensDaFatura
                .Where(x => FaturaCartaoCompetencia.Calcular(x.DataEmissao, cartao.DiaFechamentoFatura, cartao.DiaVencimentoFatura).Competencia == fatura.Competencia)
                .Where(x => x.StatusContaId == StatusConta.LiquidadaId))
            {
                // Compras de cartão voltam para "Em fatura": continuam pertencendo à fatura reaberta.
                itemFatura.Estornar(itemFatura.CartaoId.HasValue ? StatusConta.EmFaturaId : StatusConta.PendenteId);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await queryService.ObterPorIdAsync(conta.Id, cancellationToken);
    }

    public async Task<ContaPagarDetalheResponse?> CancelarAsync(Guid id, CancelarContaPagarRequest? request, CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasPagar.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (conta is null) return null;

        try { conta.Cancelar(StatusConta.CanceladaId); }
        catch (InvalidOperationException ex) { throw helper.ConverterParaValidacao(ex); }

        var movimentoEconomico = await dbContext.MovimentacoesFinanceiras
            .SingleOrDefaultAsync(x => x.ContaPagarId == conta.Id && x.Natureza == NaturezaMovimentacao.Economica, cancellationToken);
        movimentoEconomico?.Cancelar(StatusMovimentacao.CanceladaId);

        if (conta.OrigemCompraPlanejadaId.HasValue)
        {
            var compraPlanejada = await dbContext.ComprasPlanejadas
                .SingleOrDefaultAsync(x => x.Id == conta.OrigemCompraPlanejadaId.Value, cancellationToken);

            if (compraPlanejada is not null)
            {
                if (request?.CancelarPlanejamentoRelacionado == true)
                {
                    compraPlanejada.CancelarPlanejamento();
                }
                else
                {
                    compraPlanejada.ReverterParaPlanejada();
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await queryService.ObterPorIdAsync(conta.Id, cancellationToken);
    }
}
