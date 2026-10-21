using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Contracts.Financeiro.ContasReceber;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

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
        var pagadoresIds = request.PagadoresAdicionaisIds?.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (pagadoresIds is { Length: >= 2 })
            return await CriarComMultiplosPagadoresAsync(request, pagadoresIds, cancellationToken);

        return await CriarSimplexAsync(request, request.PagadorId, cancellationToken);
    }

    private async Task<ContaReceberDetalheResponse> CriarSimplexAsync(
        CriarContaReceberRequest request, Guid pagadorId, CancellationToken cancellationToken)
    {
        ContaReceberSharedHelper.ValidarRecorrencia(request.DataEmissao, request.Recorrencia, request.QuantidadeParcelas);

        var liquidarNaCriacao = await helper.ValidarCriacaoOuAtualizacaoAsync(
            pagadorId, request.ResponsavelId, request.FormaPagamentoId,
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
            pagadorId, request.DataVencimento, request.FormaPagamentoId,
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

        var primeiraConta = contas.First();

        if (request.ContaVinculadaOrigemId.HasValue)
            await VincularContaPagarOrigemAsync(primeiraConta, request.ContaVinculadaOrigemId.Value, cancellationToken);

        return await queryService.ObterPorIdAsync(primeiraConta.Id, cancellationToken)
            ?? throw new InvalidOperationException("Falha ao mapear conta criada.");
    }

    private async Task<ContaReceberDetalheResponse> CriarComMultiplosPagadoresAsync(
        CriarContaReceberRequest request, Guid[] pagadoresIds, CancellationToken cancellationToken)
    {
        ContaReceberSharedHelper.ValidarRecorrencia(request.DataEmissao, request.Recorrencia, request.QuantidadeParcelas);

        await helper.ValidarCriacaoOuAtualizacaoAsync(
            pagadoresIds[0], request.ResponsavelId, request.FormaPagamentoId,
            request.CartaoId, request.ContaBancariaId, request.DataLiquidacao,
            request.QuantidadeParcelas, request.Rateios, cancellationToken);

        // Validate remaining pagadores
        for (var i = 1; i < pagadoresIds.Length; i++)
        {
            _ = await dbContext.Pessoas.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == pagadoresIds[i], cancellationToken)
                ?? throw ValidationExceptionFactory.Create("PagadoresAdicionaisIds", $"Pagador {i + 1} não encontrado.");
        }

        var grupoResponsaveisId = Guid.NewGuid();
        var valoresPorPagador = (request.ValoresPorPagador?.Count == pagadoresIds.Length)
            ? request.ValoresPorPagador.ToArray()
            : ParcelamentoHelper.Distribuir(request.ValorOriginal, pagadoresIds.Length).ToArray();
        var baseRateios = ContaReceberSharedHelper.ConverterRateios(request.Rateios);
        var valorBase = request.ValorOriginal > 0 ? request.ValorOriginal : 1m;
        var todasContas = new List<ContaReceber>();

        for (var i = 0; i < pagadoresIds.Length; i++)
        {
            var pagadorId = pagadoresIds[i];
            var valorPagador = valoresPorPagador[i];
            var rateiosPagador = ParcelamentoHelper.DistribuirRateios(baseRateios, valorPagador, valorBase);

            var contas = ContaReceber.CriarParcelas(
                request.NumeroDocumento, request.DataEmissao, request.ResponsavelId,
                pagadorId, request.DataVencimento, request.FormaPagamentoId,
                request.CartaoId, request.ContaBancariaId, valorPagador,
                request.ValorDesconto / pagadoresIds.Length,
                request.ValorJuros / pagadoresIds.Length,
                request.ValorMulta / pagadoresIds.Length,
                request.QuantidadeParcelas, request.Descricao, request.Observacao,
                StatusConta.PendenteId, false, null,
                OrigemLancamento.Manual, rateiosPagador,
                DateOnly.FromDateTime(DateTime.Today));

            foreach (var conta in contas)
                conta.DefinirGrupoResponsaveis(grupoResponsaveisId);

            todasContas.AddRange(contas);
        }

        dbContext.ContasReceber.AddRange(todasContas);
        dbContext.RateiosContaGerencial.AddRange(todasContas.SelectMany(x => x.Rateios));
        await dbContext.SaveChangesAsync(cancellationToken);

        var primeiraConta = todasContas.First();

        if (request.ContaVinculadaOrigemId.HasValue)
            await VincularContaPagarOrigemAsync(primeiraConta, request.ContaVinculadaOrigemId.Value, cancellationToken);

        return await queryService.ObterPorIdAsync(primeiraConta.Id, cancellationToken)
            ?? throw new InvalidOperationException("Falha ao mapear conta criada.");
    }

    private async Task VincularContaPagarOrigemAsync(
        ContaReceber novaConta,
        Guid contaPagarOrigemId,
        CancellationToken cancellationToken)
    {
        var contaPagar = await dbContext.ContasPagar
            .SingleOrDefaultAsync(x => x.Id == contaPagarOrigemId, cancellationToken)
            ?? throw ValidationExceptionFactory.Create(
                "ContaVinculadaOrigemId",
                "Conta a pagar de origem não encontrada.");

        novaConta.VincularContaContraria(contaPagar.Id, TipoContaVinculada.Pagar);
        contaPagar.VincularContaContraria(novaConta.Id, TipoContaVinculada.Receber);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
