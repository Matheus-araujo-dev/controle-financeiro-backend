using System.Text.Json;
using ControleFinanceiro.Application.Common.Cache;
using ControleFinanceiro.Application.Common.Exceptions;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Application.Financeiro.Recorrencias;
using ControleFinanceiro.Contracts.Financeiro.Common;
using ControleFinanceiro.Contracts.Financeiro.ContasPagar;
using ControleFinanceiro.Domain.Cadastros.Cartoes;
using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;
using ControleFinanceiro.Domain.Financeiro;
using ControleFinanceiro.Domain.PlanejamentoCompras;
using Microsoft.EntityFrameworkCore;
using TipoPeriodicidadeRecorrenciaContract = ControleFinanceiro.Contracts.Financeiro.Common.TipoPeriodicidadeRecorrencia;
using TipoPeriodicidadeRecorrenciaDomain = ControleFinanceiro.Domain.Financeiro.TipoPeriodicidadeRecorrencia;
using TipoDiaRecorrenciaContract = ControleFinanceiro.Contracts.Financeiro.Common.TipoDiaRecorrencia;
using TipoDiaRecorrenciaDomain = ControleFinanceiro.Domain.Financeiro.TipoDiaRecorrencia;

namespace ControleFinanceiro.Application.Financeiro.ContasPagar;

/// <summary>Shared validation, mapping and template helpers used across the focused ContaPagar services.</summary>
public sealed class ContaPagarSharedHelper(
    IAppDbContext dbContext,
    IValidationResultFactory validationFactory,
    ILookupCacheService lookupCache)
{
    internal async Task<ContaPagarValidationContext> ValidarCriacaoOuAtualizacaoAsync(
        DateOnly dataEmissao,
        Guid recebedorId,
        Guid? responsavelCompraId,
        Guid formaPagamentoId,
        Guid? cartaoId,
        Guid? contaBancariaId,
        DateOnly? dataLiquidacao,
        int quantidadeParcelas,
        IReadOnlyCollection<RateioRequest> rateios,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Pessoas.AnyAsync(x => x.Id == recebedorId, cancellationToken))
            throw validationFactory.Create("RecebedorId", "Recebedor não encontrado.");

        if (responsavelCompraId.HasValue &&
            !await dbContext.Pessoas.AnyAsync(x => x.Id == responsavelCompraId.Value, cancellationToken))
            throw validationFactory.Create("ResponsavelCompraId", "Responsável não encontrado.");

        var formaPagamento = await lookupCache.GetFormaPagamentoByIdAsync(formaPagamentoId, cancellationToken)
            ?? throw validationFactory.Create("FormaPagamentoId", "Forma de pagamento não encontrada.");

        if (quantidadeParcelas < 1)
            throw validationFactory.Create("QuantidadeParcelas", "Quantidade de parcelas deve ser maior que zero.");

        Cartao? cartao = null;
        if (cartaoId.HasValue)
        {
            cartao = await dbContext.Cartoes.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == cartaoId.Value, cancellationToken)
                ?? throw validationFactory.Create("CartaoId", "Cartão não encontrado.");
        }

        if (contaBancariaId.HasValue &&
            !await dbContext.ContasBancarias.AnyAsync(x => x.Id == contaBancariaId.Value, cancellationToken))
            throw validationFactory.Create("ContaBancariaId", "Conta bancária não encontrada.");

        await ContaGerencialLancamentoValidator.ValidarContasLancaveisPorTipoAsync(
            dbContext,
            rateios.Select(x => x.ContaGerencialId).ToArray(),
            TipoContaGerencial.Despesa,
            "Rateios",
            "Uma ou mais contas gerenciais não foram encontradas.",
            "Somente contas gerenciais filhas podem ser usadas em rateios.",
            "Contas a pagar aceitam apenas contas gerenciais de despesa.",
            cancellationToken);

        if (formaPagamento.EhCartao)
        {
            if (!cartaoId.HasValue)
                throw validationFactory.Create("CartaoId", "Cartão é obrigatório para compras em cartão.");

            if (contaBancariaId.HasValue)
                throw validationFactory.Create("ContaBancariaId", "Compras em cartão não geram saída bancária real neste momento.");

            if (dataLiquidacao.HasValue)
                throw validationFactory.Create("DataLiquidacao", "Compras em cartão não devem informar data de liquidação.");

            var competencia = FaturaCartaoCompetencia.Calcular(dataEmissao, cartao!.DiaFechamentoFatura, cartao.DiaVencimentoFatura);

            if (await dbContext.FaturasCartao.AnyAsync(
                    x => x.CartaoId == cartaoId.Value &&
                         x.Competencia == competencia.Competencia &&
                         x.Status == StatusFaturaCartao.Paga, cancellationToken))
                throw validationFactory.Create("DataEmissao", "Já existe fatura paga para a competência desta compra em cartão.");
        }
        else if (cartaoId.HasValue)
        {
            throw validationFactory.Create("CartaoId", "Cartão informado para uma forma de pagamento que não é cartão.");
        }

        if (formaPagamento.BaixarAutomaticamente && !formaPagamento.EhCartao && !contaBancariaId.HasValue)
            throw validationFactory.Create("ContaBancariaId", "Conta bancária é obrigatória para baixa automática.");

        if (!formaPagamento.BaixarAutomaticamente && !formaPagamento.EhCartao && dataLiquidacao.HasValue)
            throw validationFactory.Create("DataLiquidacao", "Data de liquidação só pode ser informada com baixa automática.");

        return new ContaPagarValidationContext(formaPagamento.BaixarAutomaticamente && !formaPagamento.EhCartao, formaPagamento.EhCartao, cartao);
    }

    internal async Task<PlanejamentoCompra?> ObterCompraPlanejadaOrigemAsync(Guid? origemCompraPlanejadaId, CancellationToken cancellationToken)
    {
        if (!origemCompraPlanejadaId.HasValue) return null;

        var compraPlanejada = await dbContext.ComprasPlanejadas
            .SingleOrDefaultAsync(x => x.Id == origemCompraPlanejadaId.Value, cancellationToken)
            ?? throw validationFactory.Create("OrigemCompraPlanejadaId", "Compra planejada nao encontrada.");

        if (compraPlanejada.ContaPagarGeradaId.HasValue)
            throw validationFactory.Create("OrigemCompraPlanejadaId", "Compra planejada ja foi convertida em conta a pagar.");

        if (compraPlanejada.Status == StatusPlanejamentoCompra.Cancelada)
            throw validationFactory.Create("OrigemCompraPlanejadaId", "Compra planejada cancelada nao pode ser convertida.");

        return compraPlanejada;
    }

    internal async Task<RegraRecorrencia> ObterRegraRecorrenciaObrigatoriaAsync(ContaPagar conta, CancellationToken cancellationToken)
    {
        if (!conta.RegraRecorrenciaId.HasValue)
            throw validationFactory.Create("Recorrencia", "A conta informada não possui regra de recorrência.");

        return await dbContext.RegrasRecorrencia.SingleAsync(x => x.Id == conta.RegraRecorrenciaId.Value, cancellationToken);
    }

    internal void ValidarRecorrencia(DateOnly dataEmissao, RecorrenciaConfigRequest? recorrencia, int quantidadeParcelas)
    {
        if (recorrencia is not null && quantidadeParcelas != 1)
            throw validationFactory.Create("QuantidadeParcelas", "Recorrência inicial não pode ser combinada com parcelamento.");

        if (recorrencia is null) return;

        var dataInicio = ResolveDataInicioRecorrencia(dataEmissao, recorrencia);
        var dataFim = ResolveDataFimRecorrencia(recorrencia);

        if (dataFim.HasValue && dataFim.Value < dataInicio)
            throw validationFactory.Create("Recorrencia.DataFim", "Data fim deve ser maior ou igual à primeira ocorrência da série.");
    }

    internal RegraRecorrencia CriarRegraRecorrencia(CriarContaPagarRequest request, RecorrenciaConfigRequest recorrencia) =>
        BuildRegraRecorrencia(request.DataEmissao, recorrencia, SerializarTemplate(request));

    internal RegraRecorrencia CriarRegraRecorrencia(AtualizarContaPagarRequest request, RecorrenciaConfigRequest recorrencia) =>
        BuildRegraRecorrencia(request.DataEmissao, recorrencia, SerializarTemplate(request));

    private RegraRecorrencia BuildRegraRecorrencia(DateOnly dataEmissao, RecorrenciaConfigRequest recorrencia, string templateJson) =>
        RegraRecorrencia.Criar(
            TipoLancamentoRecorrencia.ContaPagar,
            MapearTipoPeriodicidadeDominio(recorrencia.TipoPeriodicidade),
            MapearTipoDiaDominio(recorrencia.TipoDia),
            recorrencia.DiaOrdemMensal,
            ResolveDataInicioRecorrencia(dataEmissao, recorrencia),
            ResolveDataFimRecorrencia(recorrencia),
            recorrencia.PermiteEdicaoOcorrenciaIndividual,
            recorrencia.Observacao,
            templateJson);

    internal DateOnly ResolveDataInicioRecorrencia(DateOnly dataEmissao, RecorrenciaConfigRequest recorrencia)
    {
        if (recorrencia.DataInicio.HasValue)
            return RecorrenciaDateHelper.CalculateDateForReferenceMonth(recorrencia.DataInicio.Value, MapearTipoDiaDominio(recorrencia.TipoDia), recorrencia.DiaOrdemMensal);

        return RecorrenciaDateHelper.CalculateAutomaticStartDate(dataEmissao, MapearTipoDiaDominio(recorrencia.TipoDia), recorrencia.DiaOrdemMensal);
    }

    internal DateOnly? ResolveDataFimRecorrencia(RecorrenciaConfigRequest recorrencia)
    {
        if (!recorrencia.DataFim.HasValue) return null;
        return RecorrenciaDateHelper.CalculateDateForReferenceMonth(recorrencia.DataFim.Value, MapearTipoDiaDominio(recorrencia.TipoDia), recorrencia.DiaOrdemMensal);
    }

    internal TipoPeriodicidadeRecorrenciaDomain MapearTipoPeriodicidadeDominio(TipoPeriodicidadeRecorrenciaContract tipo) =>
        tipo switch
        {
            TipoPeriodicidadeRecorrenciaContract.Mensal => TipoPeriodicidadeRecorrenciaDomain.Mensal,
            _ => throw validationFactory.Create("Recorrencia.TipoPeriodicidade", "Tipo de periodicidade de recorrência inválido.")
        };

    internal static TipoPeriodicidadeRecorrenciaContract MapearTipoPeriodicidadeContrato(TipoPeriodicidadeRecorrenciaDomain tipo) =>
        tipo switch
        {
            TipoPeriodicidadeRecorrenciaDomain.Mensal => TipoPeriodicidadeRecorrenciaContract.Mensal,
            _ => throw new ArgumentOutOfRangeException(nameof(tipo))
        };

    internal TipoDiaRecorrenciaDomain MapearTipoDiaDominio(TipoDiaRecorrenciaContract tipo) =>
        tipo switch
        {
            TipoDiaRecorrenciaContract.DiaFixo => TipoDiaRecorrenciaDomain.DiaFixo,
            TipoDiaRecorrenciaContract.DiaUtil => TipoDiaRecorrenciaDomain.DiaUtil,
            _ => throw validationFactory.Create("Recorrencia.TipoDia", "Tipo de dia de recorrência inválido.")
        };

    internal static TipoDiaRecorrenciaContract MapearTipoDiaContrato(TipoDiaRecorrenciaDomain tipo) =>
        tipo switch
        {
            TipoDiaRecorrenciaDomain.DiaFixo => TipoDiaRecorrenciaContract.DiaFixo,
            TipoDiaRecorrenciaDomain.DiaUtil => TipoDiaRecorrenciaContract.DiaUtil,
            _ => throw new ArgumentOutOfRangeException(nameof(tipo))
        };

    internal IReadOnlyCollection<RateioPlano> ConverterRateios(IReadOnlyCollection<RateioRequest> rateios)
    {
        try { return rateios.Select(x => RateioPlano.Create(x.ContaGerencialId, x.Valor)).ToArray(); }
        catch (ArgumentException ex) { throw validationFactory.Create("Rateios", ex.Message); }
    }

    internal ApplicationValidationException ConverterParaValidacao(Exception exception) =>
        validationFactory.Create("Request", exception.Message);

    internal ApplicationValidationException CriarErroValidacao(string campo, string mensagem) =>
        validationFactory.Create(campo, mensagem);

    internal async Task SincronizarRateiosContaAsync(ContaPagar conta, CancellationToken cancellationToken)
    {
        var rateiosExistentes = await dbContext.RateiosContaGerencial
            .Where(x => x.ContaPagarId == conta.Id).ToListAsync(cancellationToken);
        dbContext.RateiosContaGerencial.RemoveRange(rateiosExistentes);
        dbContext.RateiosContaGerencial.AddRange(conta.Rateios);
    }

    internal async Task CancelarMovimentacaoEconomicaAsync(ContaPagar conta, CancellationToken cancellationToken)
    {
        var movimento = await dbContext.MovimentacoesFinanceiras
            .SingleOrDefaultAsync(x => x.ContaPagarId == conta.Id && x.Natureza == NaturezaMovimentacao.Economica, cancellationToken);
        movimento?.Cancelar(StatusMovimentacao.CanceladaId);
    }

    internal void AtualizarContaExistente(ContaPagar conta, AtualizarContaPagarRequest request)
    {
        try
        {
            conta.Atualizar(
                request.NumeroDocumento, request.DataEmissao, request.ResponsavelCompraId,
                request.RecebedorId, request.DataVencimento, request.FormaPagamentoId,
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

    internal static AtualizarContaPagarRequest AjustarRequestParaMes(AtualizarContaPagarRequest request, int monthOffset) =>
        request with
        {
            DataEmissao = RecorrenciaDateHelper.Shift(request.DataEmissao, monthOffset),
            DataVencimento = RecorrenciaDateHelper.Shift(request.DataVencimento, monthOffset)
        };

    internal static ContaPagar CriarOcorrenciaRecorrente(ContaPagarRecorrenciaTemplate template, Guid regraRecorrenciaId, DateOnly dataVencimento)
    {
        var monthOffset = RecorrenciaDateHelper.CalculateMonthOffset(template.DataVencimento, dataVencimento);
        return ContaPagar.Criar(
            template.NumeroDocumento,
            RecorrenciaDateHelper.Shift(template.DataEmissao, monthOffset),
            template.ResponsavelCompraId, template.RecebedorId, dataVencimento,
            template.FormaPagamentoId, template.CartaoId, template.ContaBancariaId,
            template.ValorOriginal, template.ValorDesconto, template.ValorJuros, template.ValorMulta,
            1, 1, null, null, template.Descricao, template.Observacao,
            StatusConta.PendenteId, true, regraRecorrenciaId, OrigemLancamento.Recorrencia,
            template.Rateios.Select(x => RateioPlano.Create(x.ContaGerencialId, x.Valor)).ToArray());
    }

    internal static IReadOnlyCollection<MovimentacaoFinanceira> AplicarLiquidacaoAutomatica(
        IReadOnlyCollection<ContaPagar> contas, DateOnly? dataLiquidacao, Guid contaBancariaId)
    {
        var movimentos = new List<MovimentacaoFinanceira>(contas.Count);
        foreach (var conta in contas)
        {
            var dataMovimentacao = (dataLiquidacao ?? conta.DataEmissao).AddMonths(conta.NumeroParcela - 1);
            conta.Liquidar(dataMovimentacao, contaBancariaId, StatusConta.LiquidadaId);
            movimentos.Add(MovimentacaoFinanceira.CriarLiquidacaoContaPagar(
                conta.Id, contaBancariaId, dataMovimentacao, conta.ValorLiquido,
                StatusMovimentacao.EfetivadaId, conta.Descricao));
        }
        return movimentos;
    }

    internal static string SerializarTemplate(CriarContaPagarRequest request) =>
        JsonSerializer.Serialize(new ContaPagarRecorrenciaTemplate(
            request.NumeroDocumento, request.DataEmissao, request.ResponsavelCompraId,
            request.RecebedorId, request.DataVencimento, request.FormaPagamentoId,
            request.CartaoId, request.ContaBancariaId, request.ValorOriginal,
            request.ValorDesconto, request.ValorJuros, request.ValorMulta,
            request.Descricao, request.Observacao,
            request.Rateios.Select(x => new RateioRecorrenciaTemplate(x.ContaGerencialId, x.Valor)).ToArray()));

    internal static string SerializarTemplate(AtualizarContaPagarRequest request) =>
        JsonSerializer.Serialize(new ContaPagarRecorrenciaTemplate(
            request.NumeroDocumento, request.DataEmissao, request.ResponsavelCompraId,
            request.RecebedorId, request.DataVencimento, request.FormaPagamentoId,
            request.CartaoId, request.ContaBancariaId, request.ValorOriginal,
            request.ValorDesconto, request.ValorJuros, request.ValorMulta,
            request.Descricao, request.Observacao,
            request.Rateios.Select(x => new RateioRecorrenciaTemplate(x.ContaGerencialId, x.Valor)).ToArray()));

    internal static ContaPagarRecorrenciaTemplate DesserializarTemplate(string templateJson) =>
        JsonSerializer.Deserialize<ContaPagarRecorrenciaTemplate>(templateJson)
        ?? throw new InvalidOperationException("Template de recorrência inválido.");

    internal async Task<decimal> CalcularSaldoLiquidadoAsync(Guid contaId, CancellationToken cancellationToken) =>
        await dbContext.MovimentacoesFinanceiras
            .Where(x => x.ContaPagarId == contaId &&
                        x.Natureza == NaturezaMovimentacao.Realizada &&
                        x.StatusMovimentacaoId != StatusMovimentacao.CanceladaId)
            .SumAsync(x => (decimal?)x.Valor, cancellationToken) ?? 0m;

    internal async Task<IReadOnlyCollection<RateioPlano>> RecalcularRateiosAsync(Guid contaId, decimal novoValorLiquido, CancellationToken cancellationToken)
    {
        var rateiosOriginais = await (
            from rateio in dbContext.RateiosContaGerencial.AsNoTracking()
            join cg in dbContext.ContasGerenciais.AsNoTracking() on rateio.ContaGerencialId equals cg.Id
            where rateio.ContaPagarId == contaId
            orderby cg.Descricao
            select new RateioPlano(rateio.ContaGerencialId, rateio.Valor))
            .ToArrayAsync(cancellationToken);

        if (rateiosOriginais.Length == 0)
            throw validationFactory.Create("Rateios", "Ao menos um rateio é obrigatório.");

        if (rateiosOriginais.Length == 1)
            return [RateioPlano.Create(rateiosOriginais[0].ContaGerencialId, novoValorLiquido)];

        var totalOriginal = rateiosOriginais.Sum(x => x.Valor);
        if (totalOriginal <= 0)
            throw validationFactory.Create("Rateios", "Valor base de rateio inválido.");

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

    internal async Task AtualizarTemplateRecorrenciaAsync(Guid regraRecorrenciaId, decimal novoValorLiquido, IReadOnlyCollection<RateioPlano> novosRateios, CancellationToken cancellationToken)
    {
        var regra = await dbContext.RegrasRecorrencia.SingleAsync(x => x.Id == regraRecorrenciaId, cancellationToken);
        var template = DesserializarTemplate(regra.TemplateJson);
        var novoTemplate = template with
        {
            ValorOriginal = decimal.Round(novoValorLiquido + template.ValorDesconto - template.ValorJuros - template.ValorMulta, 2, MidpointRounding.AwayFromZero),
            Rateios = novosRateios.Select(r => new RateioRecorrenciaTemplate(r.ContaGerencialId, r.Valor)).ToArray()
        };
        regra.Atualizar(regra.TipoPeriodicidade, regra.TipoDia, regra.DiaOrdemMensal, regra.DataInicio, regra.DataFim,
            regra.PermiteEdicaoOcorrenciaIndividual, regra.Observacao, JsonSerializer.Serialize(novoTemplate));
    }

    internal ILookupCacheService LookupCache => lookupCache;
    internal IAppDbContext DbContext => dbContext;
}
