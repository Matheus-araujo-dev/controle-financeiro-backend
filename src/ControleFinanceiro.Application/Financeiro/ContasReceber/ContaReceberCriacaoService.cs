using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Contracts.Financeiro.ContasReceber;
using ControleFinanceiro.Domain.Financeiro;

namespace ControleFinanceiro.Application.Financeiro.ContasReceber;

public interface IContaReceberCriacaoService
{
    Task<ContaReceberDetalheResponse> CriarAsync(CriarContaReceberRequest request, CancellationToken cancellationToken);
}

public sealed class ContaReceberCriacaoService(
    IAppDbContext dbContext,
    IContaReceberQueryService queryService,
    ContaReceberSharedHelper helper,
    IContaReceberRecorrenciaService recorrenciaService) : IContaReceberCriacaoService
{
    private static DateOnly HorizonteSeisMeses()
    {
        var horizonte = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(6);
        return new DateOnly(horizonte.Year, horizonte.Month, DateTime.DaysInMonth(horizonte.Year, horizonte.Month));
    }

    public async Task<ContaReceberDetalheResponse> CriarAsync(
        CriarContaReceberRequest request, CancellationToken cancellationToken)
    {
        ContaReceberSharedHelper.ValidarRecorrencia(request.DataEmissao, request.Recorrencia, request.QuantidadeParcelas);

        var liquidarNaCriacao = await helper.ValidarCriacaoOuAtualizacaoAsync(
            request.PagadorId, request.ResponsavelId, request.FormaPagamentoId,
            request.CartaoId, request.ContaBancariaId, request.DataLiquidacao,
            request.QuantidadeParcelas, request.Rateios, cancellationToken);

        RegraRecorrencia? regra = null;
        if (request.Recorrencia is not null)
        {
            regra = ContaReceberSharedHelper.CriarRegraRecorrencia(request, request.Recorrencia);
            dbContext.RegrasRecorrencia.Add(regra);
        }

        var contas = ContaReceber.CriarParcelas(
            request.NumeroDocumento, request.DataEmissao, request.ResponsavelId,
            request.PagadorId, request.DataVencimento, request.FormaPagamentoId,
            request.CartaoId, request.ContaBancariaId, request.ValorOriginal,
            request.ValorDesconto, request.ValorJuros, request.ValorMulta,
            request.QuantidadeParcelas, request.Descricao, request.Observacao,
            StatusConta.PendenteId, regra is not null, regra?.Id,
            OrigemLancamento.Manual,
            ContaReceberSharedHelper.ConverterRateios(request.Rateios),
            DateOnly.FromDateTime(DateTime.Today));

        dbContext.ContasReceber.AddRange(contas);
        dbContext.RateiosContaGerencial.AddRange(contas.SelectMany(x => x.Rateios));

        if (liquidarNaCriacao)
            dbContext.MovimentacoesFinanceiras.AddRange(
                ContaReceberSharedHelper.AplicarLiquidacaoAutomatica(contas, request.DataLiquidacao, request.ContaBancariaId!.Value));

        await dbContext.SaveChangesAsync(cancellationToken);

        if (regra is not null)
            await recorrenciaService.GerarPorRegraAsync(regra, HorizonteSeisMeses(), cancellationToken);

        return await queryService.ObterPorIdAsync(contas.First().Id, cancellationToken)
            ?? throw new InvalidOperationException("Falha ao mapear conta criada.");
    }
}
