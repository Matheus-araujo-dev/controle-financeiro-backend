using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Contracts.Financeiro.ContasPagar;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Financeiro.ContasPagar;

public interface IContaPagarCriacaoService
{
    Task<ContaPagarDetalheResponse> CriarAsync(CriarContaPagarRequest request, CancellationToken cancellationToken);
}

public sealed class ContaPagarCriacaoService(
    IAppDbContext dbContext,
    IContaPagarQueryService queryService,
    ContaPagarSharedHelper helper,
    IContaPagarRecorrenciaService recorrenciaService) : IContaPagarCriacaoService
{
    private static DateOnly HorizonteSeisMeses()
    {
        var horizonte = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(6);
        return new DateOnly(horizonte.Year, horizonte.Month, DateTime.DaysInMonth(horizonte.Year, horizonte.Month));
    }

    public async Task<ContaPagarDetalheResponse> CriarAsync(CriarContaPagarRequest request, CancellationToken cancellationToken)
    {
        var responsaveisIds = request.ResponsaveisAdicionaisIds?.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (responsaveisIds is { Length: >= 2 })
            return await CriarComMultiplosResponsaveisAsync(request, responsaveisIds, cancellationToken);

        return await CriarSimplexAsync(request, request.ResponsavelCompraId, cancellationToken);
    }

    private async Task<ContaPagarDetalheResponse> CriarSimplexAsync(
        CriarContaPagarRequest request, Guid? responsavelId, CancellationToken cancellationToken)
    {
        helper.ValidarRecorrencia(request.DataEmissao, request.Recorrencia, request.QuantidadeParcelas);
        var compraPlanejada = await helper.ObterCompraPlanejadaOrigemAsync(request.OrigemCompraPlanejadaId, cancellationToken);

        var contexto = await helper.ValidarCriacaoOuAtualizacaoAsync(
            request.DataEmissao, request.RecebedorId, responsavelId,
            request.FormaPagamentoId, request.CartaoId, request.ContaBancariaId,
            request.DataLiquidacao, request.QuantidadeParcelas, request.Rateios, cancellationToken,
            request.DataCompra, request.ForcarProximaFatura);

        RegraRecorrencia? regra = null;
        if (request.Recorrencia is not null)
        {
            regra = helper.CriarRegraRecorrencia(request, request.Recorrencia);
            dbContext.RegrasRecorrencia.Add(regra);
        }

        var rateios = helper.ConverterRateios(request.Rateios);
        var contas = contexto.CompraCartao
            ? ContaPagar.CriarParcelasCartao(
                request.NumeroDocumento, request.DataEmissao, responsavelId,
                request.RecebedorId, request.FormaPagamentoId, contexto.Cartao!.Id,
                request.ValorOriginal, request.ValorDesconto, request.ValorJuros, request.ValorMulta,
                request.QuantidadeParcelas, request.OrigemCompraPlanejadaId, request.Descricao,
                request.Observacao, StatusConta.EmFaturaId, regra is not null, regra?.Id,
                OrigemLancamento.Manual, rateios,
                contexto.Cartao.DiaFechamentoFatura, contexto.Cartao.DiaVencimentoFatura,
                contexto.DataCompraCartao, contexto.DataVencimentoEfetivo)
            : ContaPagar.CriarParcelas(
                request.NumeroDocumento, request.DataEmissao, responsavelId,
                request.RecebedorId, request.DataVencimento, request.FormaPagamentoId,
                request.CartaoId, request.ContaBancariaId, request.ValorOriginal,
                request.ValorDesconto, request.ValorJuros, request.ValorMulta,
                request.QuantidadeParcelas, request.OrigemCompraPlanejadaId, request.Descricao,
                request.Observacao, StatusConta.PendenteId, regra is not null, regra?.Id,
                OrigemLancamento.Manual, rateios, DateOnly.FromDateTime(DateTime.Today));

        dbContext.ContasPagar.AddRange(contas);
        dbContext.RateiosContaGerencial.AddRange(contas.SelectMany(x => x.Rateios));

        if (contexto.CompraCartao) compraPlanejada?.MarcarComoComprada();
        else compraPlanejada?.MarcarComoConvertidaEmContaPagar(contas.First().Id);

        if (contexto.LiquidarNaCriacao)
            dbContext.MovimentacoesFinanceiras.AddRange(
                ContaPagarSharedHelper.AplicarLiquidacaoAutomatica(contas, request.DataLiquidacao, request.ContaBancariaId!.Value));

        var primeiraConta = contas.First();
        await dbContext.SaveChangesAsync(cancellationToken);

        if (regra is not null)
            await recorrenciaService.GerarPorRegraAsync(regra, HorizonteSeisMeses(), cancellationToken);

        if (request.ContaVinculadaOrigemId.HasValue)
            await VincularContaReceberOrigemAsync(primeiraConta, request.ContaVinculadaOrigemId.Value, cancellationToken);

        return await queryService.ObterPorIdAsync(primeiraConta.Id, cancellationToken)
            ?? throw new InvalidOperationException("Falha ao mapear conta criada.");
    }

    private async Task<ContaPagarDetalheResponse> CriarComMultiplosResponsaveisAsync(
        CriarContaPagarRequest request, Guid[] responsaveisIds, CancellationToken cancellationToken)
    {
        helper.ValidarRecorrencia(request.DataEmissao, request.Recorrencia, request.QuantidadeParcelas);

        // Validate base context with first responsável
        var contexto = await helper.ValidarCriacaoOuAtualizacaoAsync(
            request.DataEmissao, request.RecebedorId, responsaveisIds[0],
            request.FormaPagamentoId, request.CartaoId, request.ContaBancariaId,
            request.DataLiquidacao, request.QuantidadeParcelas, request.Rateios, cancellationToken,
            request.DataCompra, request.ForcarProximaFatura);

        // Validate all other responsáveis
        for (var i = 1; i < responsaveisIds.Length; i++)
        {
            var resp = await dbContext.Pessoas.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == responsaveisIds[i], cancellationToken)
                ?? throw ValidationExceptionFactory.Create("ResponsaveisAdicionaisIds", $"Responsável {i + 1} não encontrado.");
            if (!resp.EhResponsavel)
                throw ValidationExceptionFactory.Create("ResponsaveisAdicionaisIds", $"A pessoa '{resp.Nome}' não está marcada como responsável.");
        }

        var grupoResponsaveisId = Guid.NewGuid();
        var valoresPorResponsavel = ParcelamentoHelper.Distribuir(request.ValorOriginal, responsaveisIds.Length).ToArray();
        var baseRateios = helper.ConverterRateios(request.Rateios);
        var valorLiquidoBase = request.ValorOriginal - request.ValorDesconto + request.ValorJuros + request.ValorMulta;
        var todasContas = new List<ContaPagar>();

        for (var i = 0; i < responsaveisIds.Length; i++)
        {
            var responsavelId = responsaveisIds[i];
            var valorResponsavel = valoresPorResponsavel[i];
            var rateiosParcela = ParcelamentoHelper.DistribuirRateios(baseRateios, valorResponsavel, valorLiquidoBase > 0 ? valorLiquidoBase : request.ValorOriginal);

            var contas = ContaPagar.CriarParcelas(
                request.NumeroDocumento, request.DataEmissao, responsavelId,
                request.RecebedorId, request.DataVencimento, request.FormaPagamentoId,
                request.CartaoId, request.ContaBancariaId, valorResponsavel,
                request.ValorDesconto / responsaveisIds.Length,
                request.ValorJuros / responsaveisIds.Length,
                request.ValorMulta / responsaveisIds.Length,
                request.QuantidadeParcelas, request.OrigemCompraPlanejadaId, request.Descricao,
                request.Observacao, StatusConta.PendenteId, false, null,
                OrigemLancamento.Manual, rateiosParcela, DateOnly.FromDateTime(DateTime.Today));

            foreach (var conta in contas)
                conta.DefinirGrupoResponsaveis(grupoResponsaveisId);

            todasContas.AddRange(contas);
        }

        dbContext.ContasPagar.AddRange(todasContas);
        dbContext.RateiosContaGerencial.AddRange(todasContas.SelectMany(x => x.Rateios));

        if (contexto.LiquidarNaCriacao)
            dbContext.MovimentacoesFinanceiras.AddRange(
                ContaPagarSharedHelper.AplicarLiquidacaoAutomatica(
                    todasContas.Where(c => c.ResponsavelCompraId == responsaveisIds[0]).ToList(),
                    request.DataLiquidacao, request.ContaBancariaId!.Value));

        await dbContext.SaveChangesAsync(cancellationToken);

        var primeira = todasContas.First();
        if (request.ContaVinculadaOrigemId.HasValue)
            await VincularContaReceberOrigemAsync(primeira, request.ContaVinculadaOrigemId.Value, cancellationToken);

        return await queryService.ObterPorIdAsync(primeira.Id, cancellationToken)
            ?? throw new InvalidOperationException("Falha ao mapear conta criada.");
    }

    private async Task VincularContaReceberOrigemAsync(
        ContaPagar novaConta,
        Guid contaReceberOrigemId,
        CancellationToken cancellationToken)
    {
        var contaReceber = await dbContext.ContasReceber
            .SingleOrDefaultAsync(x => x.Id == contaReceberOrigemId, cancellationToken)
            ?? throw ValidationExceptionFactory.Create(
                "ContaVinculadaOrigemId",
                "Conta a receber de origem não encontrada.");

        novaConta.VincularContaContraria(contaReceber.Id, TipoContaVinculada.Receber);
        contaReceber.VincularContaContraria(novaConta.Id, TipoContaVinculada.Pagar);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
