using System.Text.Json;
using ControleFinanceiro.Application.Common.Cache;
using ControleFinanceiro.Application.Common.Exceptions;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Application.Financeiro.Recorrencias;
using ControleFinanceiro.Contracts.Financeiro.Common;
using ControleFinanceiro.Contracts.Financeiro.ContasPagar;
using ControleFinanceiro.Contracts.Financeiro.ContasReceber;
using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;
using TipoPeriodicidadeRecorrenciaContract = ControleFinanceiro.Contracts.Financeiro.Common.TipoPeriodicidadeRecorrencia;
using TipoPeriodicidadeRecorrenciaDomain = ControleFinanceiro.Domain.Financeiro.TipoPeriodicidadeRecorrencia;
using TipoDiaRecorrenciaContract = ControleFinanceiro.Contracts.Financeiro.Common.TipoDiaRecorrencia;
using TipoDiaRecorrenciaDomain = ControleFinanceiro.Domain.Financeiro.TipoDiaRecorrencia;

namespace ControleFinanceiro.Application.Financeiro.ContasReceber;

/// <summary>Shared validation, mapping and template helpers used across the focused ContaReceber services.</summary>
public sealed class ContaReceberSharedHelper(IAppDbContext dbContext, ILookupCacheService lookupCache)
{
    // ── Instance: needs dbContext or lookupCache ──────────────────────────────

    internal async Task<bool> ValidarCriacaoOuAtualizacaoAsync(
        Guid pagadorId,
        Guid? responsavelId,
        Guid formaPagamentoId,
        Guid? cartaoId,
        Guid? contaBancariaId,
        DateOnly? dataLiquidacao,
        int quantidadeParcelas,
        IReadOnlyCollection<RateioRequest> rateios,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Pessoas.AnyAsync(x => x.Id == pagadorId, cancellationToken))
            throw ValidationExceptionFactory.Create("PagadorId", "Pagador não encontrado.");

        if (responsavelId.HasValue &&
            !await dbContext.Pessoas.AnyAsync(x => x.Id == responsavelId.Value, cancellationToken))
            throw ValidationExceptionFactory.Create("ResponsavelId", "Responsável não encontrado.");

        var formaPagamento = await lookupCache.GetFormaPagamentoByIdAsync(formaPagamentoId, cancellationToken);
        if (formaPagamento is null)
            throw ValidationExceptionFactory.Create("FormaPagamentoId", "Forma de pagamento não encontrada.");

        if (quantidadeParcelas < 1)
            throw ValidationExceptionFactory.Create("QuantidadeParcelas", "Quantidade de parcelas deve ser maior que zero.");

        if (cartaoId.HasValue &&
            !await dbContext.Cartoes.AnyAsync(x => x.Id == cartaoId.Value, cancellationToken))
            throw ValidationExceptionFactory.Create("CartaoId", "Cartão não encontrado.");

        if (contaBancariaId.HasValue &&
            !await dbContext.ContasBancarias.AnyAsync(x => x.Id == contaBancariaId.Value, cancellationToken))
            throw ValidationExceptionFactory.Create("ContaBancariaId", "Conta bancária não encontrada.");

        await ContaGerencialLancamentoValidator.ValidarContasLancaveisPorTipoAsync(
            dbContext,
            rateios.Select(x => x.ContaGerencialId).ToArray(),
            TipoContaGerencial.Receita,
            "Rateios",
            "Uma ou mais contas gerenciais não foram encontradas.",
            "Somente contas gerenciais filhas podem ser usadas em rateios.",
            "Contas a receber aceitam apenas contas gerenciais de receita.",
            cancellationToken);

        if (!formaPagamento.BaixarAutomaticamente && dataLiquidacao.HasValue)
            throw ValidationExceptionFactory.Create("DataLiquidacao", "Data de liquidação só pode ser informada com baixa automática.");

        if (formaPagamento.BaixarAutomaticamente && !contaBancariaId.HasValue)
            throw ValidationExceptionFactory.Create("ContaBancariaId", "Conta bancária é obrigatória para baixa automática.");

        return formaPagamento.BaixarAutomaticamente;
    }

    internal async Task<RegraRecorrencia> ObterRegraRecorrenciaObrigatoriaAsync(
        ContaReceber conta, CancellationToken cancellationToken)
    {
        if (!conta.RegraRecorrenciaId.HasValue)
            throw ValidationExceptionFactory.Create("Recorrencia", "A conta informada não possui regra de recorrência.");

        return await dbContext.RegrasRecorrencia.SingleAsync(x => x.Id == conta.RegraRecorrenciaId.Value, cancellationToken);
    }

    internal async Task SincronizarRateiosContaAsync(ContaReceber conta, CancellationToken cancellationToken)
    {
        var existentes = await dbContext.RateiosContaGerencial
            .Where(x => x.ContaReceberId == conta.Id)
            .ToListAsync(cancellationToken);
        dbContext.RateiosContaGerencial.RemoveRange(existentes);
        dbContext.RateiosContaGerencial.AddRange(conta.Rateios);
    }

    internal async Task<decimal> CalcularSaldoLiquidadoAsync(Guid contaId, CancellationToken cancellationToken) =>
        await dbContext.MovimentacoesFinanceiras
            .Where(x => x.ContaReceberId == contaId &&
                        x.Natureza == NaturezaMovimentacao.Realizada &&
                        x.StatusMovimentacaoId != StatusMovimentacao.CanceladaId)
            .SumAsync(x => (decimal?)x.Valor, cancellationToken) ?? 0m;

    internal async Task<IReadOnlyCollection<RateioPlano>> RecalcularRateiosAsync(
        Guid contaId, decimal novoValorLiquido, CancellationToken cancellationToken)
    {
        var rateiosOriginais = await (
            from rateio in dbContext.RateiosContaGerencial.AsNoTracking()
            join cg in dbContext.ContasGerenciais.AsNoTracking() on rateio.ContaGerencialId equals cg.Id
            where rateio.ContaReceberId == contaId
            orderby cg.Descricao
            select new RateioPlano(rateio.ContaGerencialId, rateio.Valor))
            .ToArrayAsync(cancellationToken);

        if (rateiosOriginais.Length == 0)
            throw ValidationExceptionFactory.Create("Rateios", "Ao menos um rateio é obrigatório.");

        if (rateiosOriginais.Length == 1)
            return [RateioPlano.Create(rateiosOriginais[0].ContaGerencialId, novoValorLiquido)];

        var totalOriginal = rateiosOriginais.Sum(x => x.Valor);
        if (totalOriginal <= 0)
            throw ValidationExceptionFactory.Create("Rateios", "Valor base de rateio inválido.");

        var planos = new List<RateioPlano>(rateiosOriginais.Length);
        decimal acumulado = 0m;

        for (var i = 0; i < rateiosOriginais.Length - 1; i++)
        {
            var rateio = rateiosOriginais[i];
            var valor = decimal.Round(novoValorLiquido * (rateio.Valor / totalOriginal), 2, MidpointRounding.AwayFromZero);
            acumulado += valor;
            planos.Add(RateioPlano.Create(rateio.ContaGerencialId, valor));
        }

        var ultimo = rateiosOriginais[^1];
        planos.Add(RateioPlano.Create(ultimo.ContaGerencialId, decimal.Round(novoValorLiquido - acumulado, 2, MidpointRounding.AwayFromZero)));
        return planos;
    }

    internal async Task AtualizarTemplateRecorrenciaAsync(
        Guid regraRecorrenciaId, decimal novoValorLiquido,
        IReadOnlyCollection<RateioPlano> novosRateios, CancellationToken cancellationToken)
    {
        var regra = await dbContext.RegrasRecorrencia.SingleAsync(x => x.Id == regraRecorrenciaId, cancellationToken);
        var template = DesserializarTemplate(regra.TemplateJson);
        var novoTemplate = template with
        {
            ValorOriginal = decimal.Round(novoValorLiquido + template.ValorDesconto - template.ValorJuros - template.ValorMulta, 2, MidpointRounding.AwayFromZero),
            Rateios = novosRateios.Select(r => new RateioRecorrenciaTemplate(r.ContaGerencialId, r.Valor)).ToArray()
        };
        regra.Atualizar(
            regra.TipoPeriodicidade, regra.TipoDia, regra.DiaOrdemMensal,
            regra.DataInicio, regra.DataFim,
            regra.PermiteEdicaoOcorrenciaIndividual, regra.Observacao,
            JsonSerializer.Serialize(novoTemplate));
    }

    internal ILookupCacheService LookupCache => lookupCache;
    internal IAppDbContext DbContext => dbContext;

    // ── Static helpers ────────────────────────────────────────────────────────

    internal static IReadOnlyCollection<RateioPlano> ConverterRateios(IReadOnlyCollection<RateioRequest> rateios)
    {
        try { return rateios.Select(x => RateioPlano.Create(x.ContaGerencialId, x.Valor)).ToArray(); }
        catch (ArgumentException ex) { throw ValidationExceptionFactory.Create("Rateios", ex.Message); }
    }

    internal static IReadOnlyCollection<MovimentacaoFinanceira> AplicarLiquidacaoAutomatica(
        IReadOnlyCollection<ContaReceber> contas, DateOnly? dataLiquidacao, Guid contaBancariaId)
    {
        var movimentos = new List<MovimentacaoFinanceira>(contas.Count);
        foreach (var conta in contas)
        {
            var data = (dataLiquidacao ?? conta.DataEmissao).AddMonths(conta.NumeroParcela - 1);
            conta.Liquidar(data, contaBancariaId, StatusConta.LiquidadaId);
            movimentos.Add(MovimentacaoFinanceira.CriarLiquidacaoContaReceber(
                conta.Id, contaBancariaId, data, conta.ValorLiquido,
                StatusMovimentacao.EfetivadaId, conta.Descricao));
        }
        return movimentos;
    }

    internal static ApplicationValidationException ConverterParaValidacao(Exception exception) =>
        ValidationExceptionFactory.Create("Request", exception.Message);

    internal static TipoPeriodicidadeRecorrenciaDomain MapearTipoPeriodicidadeDominio(TipoPeriodicidadeRecorrenciaContract tipo) =>
        tipo switch
        {
            TipoPeriodicidadeRecorrenciaContract.Mensal => TipoPeriodicidadeRecorrenciaDomain.Mensal,
            _ => throw ValidationExceptionFactory.Create("Recorrencia.TipoPeriodicidade", "Tipo de periodicidade de recorrência inválido.")
        };

    internal static TipoPeriodicidadeRecorrenciaContract MapearTipoPeriodicidadeContrato(TipoPeriodicidadeRecorrenciaDomain tipo) =>
        tipo switch
        {
            TipoPeriodicidadeRecorrenciaDomain.Mensal => TipoPeriodicidadeRecorrenciaContract.Mensal,
            _ => throw new ArgumentOutOfRangeException(nameof(tipo))
        };

    internal static TipoDiaRecorrenciaDomain MapearTipoDiaDominio(TipoDiaRecorrenciaContract tipo) =>
        tipo switch
        {
            TipoDiaRecorrenciaContract.DiaFixo => TipoDiaRecorrenciaDomain.DiaFixo,
            TipoDiaRecorrenciaContract.DiaUtil => TipoDiaRecorrenciaDomain.DiaUtil,
            _ => throw ValidationExceptionFactory.Create("Recorrencia.TipoDia", "Tipo de dia de recorrência inválido.")
        };

    internal static TipoDiaRecorrenciaContract MapearTipoDiaContrato(TipoDiaRecorrenciaDomain tipo) =>
        tipo switch
        {
            TipoDiaRecorrenciaDomain.DiaFixo => TipoDiaRecorrenciaContract.DiaFixo,
            TipoDiaRecorrenciaDomain.DiaUtil => TipoDiaRecorrenciaContract.DiaUtil,
            _ => throw new ArgumentOutOfRangeException(nameof(tipo))
        };

    internal static void ValidarRecorrencia(DateOnly dataEmissao, RecorrenciaConfigRequest? recorrencia, int quantidadeParcelas)
    {
        if (recorrencia is not null && quantidadeParcelas != 1)
            throw ValidationExceptionFactory.Create("QuantidadeParcelas", "Recorrência inicial não pode ser combinada com parcelamento.");

        if (recorrencia is null) return;

        var dataInicio = ResolveDataInicioRecorrencia(dataEmissao, recorrencia);
        var dataFim = ResolveDataFimRecorrencia(recorrencia);

        if (dataFim.HasValue && dataFim.Value < dataInicio)
            throw ValidationExceptionFactory.Create("Recorrencia.DataFim", "Data fim deve ser maior ou igual à primeira ocorrência da série.");
    }

    internal static DateOnly ResolveDataInicioRecorrencia(DateOnly dataEmissao, RecorrenciaConfigRequest recorrencia)
    {
        if (recorrencia.DataInicio.HasValue)
            return RecorrenciaDateHelper.CalculateDateForReferenceMonth(
                recorrencia.DataInicio.Value, MapearTipoDiaDominio(recorrencia.TipoDia), recorrencia.DiaOrdemMensal);

        return RecorrenciaDateHelper.CalculateAutomaticStartDate(
            dataEmissao, MapearTipoDiaDominio(recorrencia.TipoDia), recorrencia.DiaOrdemMensal);
    }

    internal static DateOnly? ResolveDataFimRecorrencia(RecorrenciaConfigRequest recorrencia)
    {
        if (!recorrencia.DataFim.HasValue) return null;
        return RecorrenciaDateHelper.CalculateDateForReferenceMonth(
            recorrencia.DataFim.Value, MapearTipoDiaDominio(recorrencia.TipoDia), recorrencia.DiaOrdemMensal);
    }

    internal static RegraRecorrencia CriarRegraRecorrencia(CriarContaReceberRequest request, RecorrenciaConfigRequest recorrencia) =>
        BuildRegraRecorrencia(request.DataEmissao, recorrencia, SerializarTemplate(request));

    internal static RegraRecorrencia CriarRegraRecorrencia(AtualizarContaReceberRequest request, RecorrenciaConfigRequest recorrencia) =>
        BuildRegraRecorrencia(request.DataEmissao, recorrencia, SerializarTemplate(request));

    private static RegraRecorrencia BuildRegraRecorrencia(DateOnly dataEmissao, RecorrenciaConfigRequest recorrencia, string templateJson) =>
        RegraRecorrencia.Criar(
            TipoLancamentoRecorrencia.ContaReceber,
            MapearTipoPeriodicidadeDominio(recorrencia.TipoPeriodicidade),
            MapearTipoDiaDominio(recorrencia.TipoDia),
            recorrencia.DiaOrdemMensal,
            ResolveDataInicioRecorrencia(dataEmissao, recorrencia),
            ResolveDataFimRecorrencia(recorrencia),
            recorrencia.PermiteEdicaoOcorrenciaIndividual,
            recorrencia.Observacao,
            templateJson);

    internal static string SerializarTemplate(CriarContaReceberRequest request) =>
        JsonSerializer.Serialize(new ContaReceberRecorrenciaTemplate(
            request.NumeroDocumento, request.DataEmissao, request.ResponsavelId,
            request.PagadorId, request.DataVencimento, request.FormaPagamentoId,
            request.CartaoId, request.ContaBancariaId, request.ValorOriginal,
            request.ValorDesconto, request.ValorJuros, request.ValorMulta,
            request.Descricao, request.Observacao,
            request.Rateios.Select(x => new RateioRecorrenciaTemplate(x.ContaGerencialId, x.Valor)).ToArray()));

    internal static string SerializarTemplate(AtualizarContaReceberRequest request) =>
        JsonSerializer.Serialize(new ContaReceberRecorrenciaTemplate(
            request.NumeroDocumento, request.DataEmissao, request.ResponsavelId,
            request.PagadorId, request.DataVencimento, request.FormaPagamentoId,
            request.CartaoId, request.ContaBancariaId, request.ValorOriginal,
            request.ValorDesconto, request.ValorJuros, request.ValorMulta,
            request.Descricao, request.Observacao,
            request.Rateios.Select(x => new RateioRecorrenciaTemplate(x.ContaGerencialId, x.Valor)).ToArray()));

    internal static ContaReceberRecorrenciaTemplate DesserializarTemplate(string templateJson) =>
        JsonSerializer.Deserialize<ContaReceberRecorrenciaTemplate>(templateJson)
        ?? throw new InvalidOperationException("Template de recorrência inválido.");

    internal static AtualizarContaReceberRequest AjustarRequestParaMes(AtualizarContaReceberRequest request, int monthOffset) =>
        request with
        {
            DataEmissao = RecorrenciaDateHelper.Shift(request.DataEmissao, monthOffset),
            DataVencimento = RecorrenciaDateHelper.Shift(request.DataVencimento, monthOffset)
        };

    internal static LancamentoOrigem MapearOrigem(OrigemLancamento origem) =>
        origem switch
        {
            OrigemLancamento.Manual => LancamentoOrigem.Manual,
            OrigemLancamento.Recorrencia => LancamentoOrigem.Recorrencia,
            OrigemLancamento.Importacao => LancamentoOrigem.Importacao,
            _ => throw new ArgumentOutOfRangeException(nameof(origem))
        };

    internal static RecorrenciaResponse? MapearRecorrencia(RegraRecorrencia? regra) =>
        regra is null
            ? null
            : new RecorrenciaResponse(
                regra.Id,
                MapearTipoPeriodicidadeContrato(regra.TipoPeriodicidade),
                MapearTipoDiaContrato(regra.TipoDia),
                regra.DiaOrdemMensal,
                regra.DataInicio,
                regra.DataFim,
                regra.Ativa,
                regra.PermiteEdicaoOcorrenciaIndividual,
                regra.Observacao);

    internal static ContaReceber CriarOcorrenciaRecorrente(
        ContaReceberRecorrenciaTemplate template, Guid regraRecorrenciaId, DateOnly dataVencimento)
    {
        var monthOffset = RecorrenciaDateHelper.CalculateMonthOffset(template.DataVencimento, dataVencimento);
        return ContaReceber.Criar(
            template.NumeroDocumento,
            RecorrenciaDateHelper.Shift(template.DataEmissao, monthOffset),
            template.ResponsavelId, template.PagadorId, dataVencimento,
            template.FormaPagamentoId, template.CartaoId, template.ContaBancariaId,
            template.ValorOriginal, template.ValorDesconto, template.ValorJuros, template.ValorMulta,
            1, 1, null, template.Descricao, template.Observacao,
            StatusConta.PendenteId, true, regraRecorrenciaId, OrigemLancamento.Recorrencia,
            template.Rateios.Select(x => RateioPlano.Create(x.ContaGerencialId, x.Valor)).ToArray());
    }

    internal static void AtualizarContaExistente(ContaReceber conta, AtualizarContaReceberRequest request)
    {
        try
        {
            conta.Atualizar(
                request.NumeroDocumento, request.DataEmissao, request.ResponsavelId,
                request.PagadorId, request.DataVencimento, request.FormaPagamentoId,
                request.CartaoId, request.ContaBancariaId, request.ValorOriginal,
                request.ValorDesconto, request.ValorJuros, request.ValorMulta,
                request.Descricao, request.Observacao, StatusConta.PendenteId,
                ConverterRateios(request.Rateios));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            throw ConverterParaValidacao(ex);
        }
    }
}
