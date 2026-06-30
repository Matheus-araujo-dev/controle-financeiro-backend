using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Contracts.Financeiro.ContasPagar;
using ControleFinanceiro.Domain.Financeiro;

namespace ControleFinanceiro.Application.Financeiro.ContasPagar;

public interface IContaPagarCriacaoService
{
    Task<ContaPagarDetalheResponse> CriarAsync(CriarContaPagarRequest request, CancellationToken cancellationToken);
}

public sealed class ContaPagarCriacaoService(
    IAppDbContext dbContext,
    IContaPagarQueryService queryService,
    ContaPagarSharedHelper helper) : IContaPagarCriacaoService
{
    public async Task<ContaPagarDetalheResponse> CriarAsync(CriarContaPagarRequest request, CancellationToken cancellationToken)
    {
        helper.ValidarRecorrencia(request.DataEmissao, request.Recorrencia, request.QuantidadeParcelas);
        var compraPlanejada = await helper.ObterCompraPlanejadaOrigemAsync(request.OrigemCompraPlanejadaId, cancellationToken);

        var contexto = await helper.ValidarCriacaoOuAtualizacaoAsync(
            request.DataEmissao, request.RecebedorId, request.ResponsavelCompraId,
            request.FormaPagamentoId, request.CartaoId, request.ContaBancariaId,
            request.DataLiquidacao, request.QuantidadeParcelas, request.Rateios, cancellationToken);

        RegraRecorrencia? regra = null;
        if (request.Recorrencia is not null)
        {
            regra = helper.CriarRegraRecorrencia(request, request.Recorrencia);
            dbContext.RegrasRecorrencia.Add(regra);
        }

        var rateios = helper.ConverterRateios(request.Rateios);
        var contas = contexto.CompraCartao
            ? ContaPagar.CriarParcelasCartao(
                request.NumeroDocumento, request.DataEmissao, request.ResponsavelCompraId,
                request.RecebedorId, request.FormaPagamentoId, contexto.Cartao!.Id,
                request.ValorOriginal, request.ValorDesconto, request.ValorJuros, request.ValorMulta,
                request.QuantidadeParcelas, request.OrigemCompraPlanejadaId, request.Descricao,
                request.Observacao, StatusConta.EmFaturaId, regra is not null, regra?.Id,
                OrigemLancamento.Manual, rateios,
                contexto.Cartao.DiaFechamentoFatura, contexto.Cartao.DiaVencimentoFatura)
            : ContaPagar.CriarParcelas(
                request.NumeroDocumento, request.DataEmissao, request.ResponsavelCompraId,
                request.RecebedorId, request.DataVencimento, request.FormaPagamentoId,
                request.CartaoId, request.ContaBancariaId, request.ValorOriginal,
                request.ValorDesconto, request.ValorJuros, request.ValorMulta,
                request.QuantidadeParcelas, request.OrigemCompraPlanejadaId, request.Descricao,
                request.Observacao, StatusConta.PendenteId, regra is not null, regra?.Id,
                OrigemLancamento.Manual, rateios);

        dbContext.ContasPagar.AddRange(contas);
        dbContext.RateiosContaGerencial.AddRange(contas.SelectMany(x => x.Rateios));

        if (contexto.CompraCartao) compraPlanejada?.MarcarComoComprada();
        else compraPlanejada?.MarcarComoConvertidaEmContaPagar(contas.First().Id);

        if (contexto.LiquidarNaCriacao)
            dbContext.MovimentacoesFinanceiras.AddRange(
                ContaPagarSharedHelper.AplicarLiquidacaoAutomatica(contas, request.DataLiquidacao, request.ContaBancariaId!.Value));

        var primeiraConta = contas.First();
        await dbContext.SaveChangesAsync(cancellationToken);

        return await queryService.ObterPorIdAsync(primeiraConta.Id, cancellationToken)
            ?? throw new InvalidOperationException("Falha ao mapear conta criada.");
    }
}
