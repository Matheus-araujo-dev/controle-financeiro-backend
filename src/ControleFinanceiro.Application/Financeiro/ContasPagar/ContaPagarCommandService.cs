using System.Text.Json;
using ControleFinanceiro.Application.Common.Cache;
using ControleFinanceiro.Application.Common.Exceptions;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Application.Financeiro.Recorrencias;
using ControleFinanceiro.Contracts.Financeiro.Common;
using ControleFinanceiro.Contracts.Financeiro.ContasPagar;
using ControleFinanceiro.Domain.Cadastros.Cartoes;
using ControleFinanceiro.Domain.Cadastros.ContasBancarias;
using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;
using ControleFinanceiro.Domain.Cadastros.FormasPagamento;
using ControleFinanceiro.Domain.Cadastros.Pessoas;
using ControleFinanceiro.Domain.Events;
using ControleFinanceiro.Domain.Financeiro;
using ControleFinanceiro.Domain.Financeiro.Events;
using ControleFinanceiro.Domain.PlanejamentoCompras;
using Microsoft.EntityFrameworkCore;
using TipoPeriodicidadeRecorrenciaContract = ControleFinanceiro.Contracts.Financeiro.Common.TipoPeriodicidadeRecorrencia;
using TipoPeriodicidadeRecorrenciaDomain = ControleFinanceiro.Domain.Financeiro.TipoPeriodicidadeRecorrencia;
using TipoDiaRecorrenciaContract = ControleFinanceiro.Contracts.Financeiro.Common.TipoDiaRecorrencia;
using TipoDiaRecorrenciaDomain = ControleFinanceiro.Domain.Financeiro.TipoDiaRecorrencia;

namespace ControleFinanceiro.Application.Financeiro.ContasPagar;

public sealed class ContaPagarCommandService(
    IAppDbContext dbContext,
    IContaPagarQueryService queryService,
    IValidationResultFactory validationFactory,
    ILookupCacheService lookupCache,
    IDomainEventDispatcher eventDispatcher) : IContaPagarCommandService
{
    private readonly IContaPagarQueryService _queryService = queryService;
    private readonly IValidationResultFactory _validationFactory = validationFactory;
    private readonly ILookupCacheService _lookupCache = lookupCache;
    private readonly IDomainEventDispatcher _eventDispatcher = eventDispatcher;

    public async Task<ContaPagarDetalheResponse> CriarAsync(CriarContaPagarRequest request, CancellationToken cancellationToken)
    {
        ValidarRecorrencia(request.DataEmissao, request.Recorrencia, request.QuantidadeParcelas);
        var compraPlanejada = await ObterCompraPlanejadaOrigemAsync(request.OrigemCompraPlanejadaId, cancellationToken);

        var contexto = await ValidarCriacaoOuAtualizacaoAsync(
            request.DataEmissao,
            request.RecebedorId,
            request.ResponsavelCompraId,
            request.FormaPagamentoId,
            request.CartaoId,
            request.ContaBancariaId,
            request.DataLiquidacao,
            request.QuantidadeParcelas,
            request.Rateios,
            cancellationToken);

        RegraRecorrencia? regra = null;
        if (request.Recorrencia is not null)
        {
            regra = CriarRegraRecorrencia(request, request.Recorrencia);
            dbContext.RegrasRecorrencia.Add(regra);
        }

        var rateios = ConverterRateios(request.Rateios);
        var contas = contexto.CompraCartao
            ? ContaPagar.CriarParcelasCartao(
                request.NumeroDocumento,
                request.DataEmissao,
                request.ResponsavelCompraId,
                request.RecebedorId,
                request.FormaPagamentoId,
                contexto.Cartao!.Id,
                request.ValorOriginal,
                request.ValorDesconto,
                request.ValorJuros,
                request.ValorMulta,
                request.QuantidadeParcelas,
                request.OrigemCompraPlanejadaId,
                request.Descricao,
                request.Observacao,
                StatusConta.EmFaturaId,
                regra is not null,
                regra?.Id,
                OrigemLancamento.Manual,
                rateios,
                contexto.Cartao.DiaFechamentoFatura,
                contexto.Cartao.DiaVencimentoFatura)
            : ContaPagar.CriarParcelas(
                request.NumeroDocumento,
                request.DataEmissao,
                request.ResponsavelCompraId,
                request.RecebedorId,
                request.DataVencimento,
                request.FormaPagamentoId,
                request.CartaoId,
                request.ContaBancariaId,
                request.ValorOriginal,
                request.ValorDesconto,
                request.ValorJuros,
                request.ValorMulta,
                request.QuantidadeParcelas,
                request.OrigemCompraPlanejadaId,
                request.Descricao,
                request.Observacao,
                StatusConta.PendenteId,
                regra is not null,
                regra?.Id,
                OrigemLancamento.Manual,
                rateios);

        dbContext.ContasPagar.AddRange(contas);
        dbContext.RateiosContaGerencial.AddRange(contas.SelectMany(x => x.Rateios));
        if (contexto.CompraCartao)
        {
            compraPlanejada?.MarcarComoComprada();
        }
        else
        {
            compraPlanejada?.MarcarComoConvertidaEmContaPagar(contas.First().Id);
        }

        if (contexto.LiquidarNaCriacao)
        {
            dbContext.MovimentacoesFinanceiras.AddRange(AplicarLiquidacaoAutomatica(contas, request.DataLiquidacao, request.ContaBancariaId!.Value));
        }
        var PrimeiraConta = contas.First();
        await _eventDispatcher.DispatchAsync(
            new ContaPagarCriadaEvent(
                PrimeiraConta.Id,
                PrimeiraConta.NumeroDocumento,
                PrimeiraConta.RecebedorId,
                PrimeiraConta.Descricao,
                PrimeiraConta.ValorLiquido,
                PrimeiraConta.DataVencimento,
                PrimeiraConta.QuantidadeParcelas,
                PrimeiraConta.NumeroParcela,
                PrimeiraConta.GrupoParcelamentoId,
                PrimeiraConta.EhRecorrente,
                PrimeiraConta.RegraRecorrenciaId),
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return await _queryService.ObterPorIdAsync(PrimeiraConta.Id, cancellationToken)
            ?? throw new InvalidOperationException("Falha ao mapear conta criada.");
    }

    public async Task<ContaPagarDetalheResponse?> AtualizarAsync(
        Guid id,
        AtualizarContaPagarRequest request,
        CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasPagar.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (conta is null)
        {
            return null;
        }

        if (request.QuantidadeParcelas != conta.QuantidadeParcelas)
        {
            throw _validationFactory.Create("QuantidadeParcelas", "Não é permitido alterar o parcelamento na edição.");
        }

        if (conta.RegraRecorrenciaId.HasValue)
        {
            var regra = await dbContext.RegrasRecorrencia
                .SingleAsync(x => x.Id == conta.RegraRecorrenciaId.Value, cancellationToken);

            if (!regra.PermiteEdicaoOcorrenciaIndividual)
            {
                throw _validationFactory.Create("Recorrencia", "A regra atual não permite edição pontual da ocorrência.");
            }

            if (request.Recorrencia is not null)
            {
                var dataInicioRecorrencia = ResolveDataInicioRecorrencia(request.DataEmissao, request.Recorrencia);
                var dataFimRecorrencia = ResolveDataFimRecorrencia(request.Recorrencia);

                regra.Atualizar(
                    MapearTipoPeriodicidadeDominio(request.Recorrencia.TipoPeriodicidade),
                    MapearTipoDiaDominio(request.Recorrencia.TipoDia),
                    request.Recorrencia.DiaOrdemMensal,
                    dataInicioRecorrencia,
                    dataFimRecorrencia,
                    request.Recorrencia.PermiteEdicaoOcorrenciaIndividual,
                    request.Recorrencia.Observacao,
                    SerializarTemplate(request));
            }
        }

        var contexto = await ValidarCriacaoOuAtualizacaoAsync(
            request.DataEmissao,
            request.RecebedorId,
            request.ResponsavelCompraId,
            request.FormaPagamentoId,
            request.CartaoId,
            request.ContaBancariaId,
            request.DataLiquidacao,
            request.QuantidadeParcelas,
            request.Rateios,
            cancellationToken);

        AtualizarContaExistente(conta, request);

        await SincronizarRateiosContaAsync(conta, cancellationToken);

        if (contexto.LiquidarNaCriacao &&
            !await dbContext.MovimentacoesFinanceiras.AnyAsync(x => x.ContaPagarId == conta.Id, cancellationToken))
        {
            dbContext.MovimentacoesFinanceiras.AddRange(
                AplicarLiquidacaoAutomatica([conta], request.DataLiquidacao, request.ContaBancariaId!.Value));
        }

        await CancelarMovimentacaoEconomicaAsync(conta, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return await _queryService.ObterPorIdAsync(conta.Id, cancellationToken);
    }

    public async Task<ContaPagarDetalheResponse?> AlterarFuturasAsync(
        Guid id,
        AtualizarContaPagarRequest request,
        CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasPagar.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (conta is null)
        {
            return null;
        }

        var regra = await ObterRegraRecorrenciaObrigatoriaAsync(conta, cancellationToken);
        ValidarRecorrencia(request.DataEmissao, request.Recorrencia, request.QuantidadeParcelas);

        var recorrencia = request.Recorrencia ?? new RecorrenciaConfigRequest(
            MapearTipoPeriodicidadeContrato(regra.TipoPeriodicidade),
            MapearTipoDiaContrato(regra.TipoDia),
            regra.DiaOrdemMensal,
            regra.DataInicio,
            regra.DataFim,
            regra.PermiteEdicaoOcorrenciaIndividual,
            regra.Observacao);

        await ValidarCriacaoOuAtualizacaoAsync(
            request.DataEmissao,
            request.RecebedorId,
            request.ResponsavelCompraId,
            request.FormaPagamentoId,
            request.CartaoId,
            request.ContaBancariaId,
            request.DataLiquidacao,
            request.QuantidadeParcelas,
            request.Rateios,
            cancellationToken);

        var dataInicioRecorrencia = ResolveDataInicioRecorrencia(request.DataEmissao, recorrencia);
        var dataFimRecorrencia = ResolveDataFimRecorrencia(recorrencia);

        regra.Atualizar(
            MapearTipoPeriodicidadeDominio(recorrencia.TipoPeriodicidade),
            MapearTipoDiaDominio(recorrencia.TipoDia),
            recorrencia.DiaOrdemMensal,
            dataInicioRecorrencia,
            dataFimRecorrencia,
            recorrencia.PermiteEdicaoOcorrenciaIndividual,
            recorrencia.Observacao,
            SerializarTemplate(request));

        var contasFuturas = await dbContext.ContasPagar
            .Where(x =>
                x.RegraRecorrenciaId == regra.Id &&
                x.DataVencimento >= conta.DataVencimento &&
                x.StatusContaId != StatusConta.LiquidadaId &&
                x.StatusContaId != StatusConta.CanceladaId)
            .OrderBy(x => x.DataVencimento)
            .ToListAsync(cancellationToken);

        foreach (var contaFutura in contasFuturas)
        {
            var mesOffset = RecorrenciaDateHelper.CalculateMonthOffset(conta.DataVencimento, contaFutura.DataVencimento);
            var requestAjustado = AjustarRequestParaMes(request, mesOffset);

            AtualizarContaExistente(contaFutura, requestAjustado);
            await SincronizarRateiosContaAsync(contaFutura, cancellationToken);

            var contexto = await ValidarCriacaoOuAtualizacaoAsync(
                requestAjustado.DataEmissao,
                requestAjustado.RecebedorId,
                requestAjustado.ResponsavelCompraId,
                requestAjustado.FormaPagamentoId,
                requestAjustado.CartaoId,
                requestAjustado.ContaBancariaId,
                requestAjustado.DataLiquidacao,
                requestAjustado.QuantidadeParcelas,
                requestAjustado.Rateios,
                cancellationToken);

            await CancelarMovimentacaoEconomicaAsync(contaFutura, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await _queryService.ObterPorIdAsync(conta.Id, cancellationToken);
    }

    public async Task<ContaPagarDetalheResponse?> GerarOcorrenciasAsync(
        Guid id,
        GerarOcorrenciasRecorrenciaRequest request,
        CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasPagar.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (conta is null)
        {
            return null;
        }

        var regra = await ObterRegraRecorrenciaObrigatoriaAsync(conta, cancellationToken);

        if (!regra.Ativa)
        {
            throw _validationFactory.Create("Recorrencia", "A recorrência está pausada ou encerrada.");
        }

        var datasExistentes = await dbContext.ContasPagar
            .Where(x => x.RegraRecorrenciaId == regra.Id)
            .Select(x => x.DataVencimento)
            .ToArrayAsync(cancellationToken);

        var datasPendentes = regra.CalcularDatasPendentes(datasExistentes, request.AteData);
        var template = DesserializarTemplate(regra.TemplateJson);

        var novasContas = datasPendentes
            .Select(dataVencimento => CriarOcorrenciaRecorrente(template, regra.Id, dataVencimento))
            .ToArray();

        dbContext.ContasPagar.AddRange(novasContas);
        dbContext.RateiosContaGerencial.AddRange(novasContas.SelectMany(x => x.Rateios));

        await dbContext.SaveChangesAsync(cancellationToken);
        return await _queryService.ObterPorIdAsync(conta.Id, cancellationToken);
    }

    public async Task<ContaPagarDetalheResponse?> PausarRecorrenciaAsync(Guid id, CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasPagar.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (conta is null)
        {
            return null;
        }

        var regra = await ObterRegraRecorrenciaObrigatoriaAsync(conta, cancellationToken);
        regra.Pausar();

        await dbContext.SaveChangesAsync(cancellationToken);
        return await _queryService.ObterPorIdAsync(conta.Id, cancellationToken);
    }

    public async Task<ContaPagarDetalheResponse?> EncerrarRecorrenciaAsync(
        Guid id,
        EncerrarRecorrenciaRequest request,
        CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasPagar.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (conta is null)
        {
            return null;
        }

        var regra = await ObterRegraRecorrenciaObrigatoriaAsync(conta, cancellationToken);
        regra.Encerrar(request.DataFim);

        var contasPosteriores = await dbContext.ContasPagar
            .Where(x =>
                x.RegraRecorrenciaId == regra.Id &&
                x.DataVencimento > request.DataFim &&
                x.StatusContaId != StatusConta.LiquidadaId &&
                x.StatusContaId != StatusConta.CanceladaId)
            .ToListAsync(cancellationToken);

        foreach (var contaPosterior in contasPosteriores)
        {
            contaPosterior.Cancelar(StatusConta.CanceladaId);

            var movimentoEconomico = await dbContext.MovimentacoesFinanceiras
                .SingleOrDefaultAsync(
                    x => x.ContaPagarId == contaPosterior.Id && x.Natureza == NaturezaMovimentacao.Economica,
                    cancellationToken);

            movimentoEconomico?.Cancelar(StatusMovimentacao.CanceladaId);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await _queryService.ObterPorIdAsync(conta.Id, cancellationToken);
    }

    public async Task<ContaPagarDetalheResponse?> LiquidarAsync(
        Guid id,
        LiquidarContaPagarRequest request,
        CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasPagar.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (conta is null)
        {
            return null;
        }

        if (conta.StatusContaId == StatusConta.LiquidadaId)
        {
            throw _validationFactory.Create("Status", "Conta já está liquidada.");
        }

        if (!await dbContext.ContasBancarias.AnyAsync(x => x.Id == request.ContaBancariaId, cancellationToken))
        {
            throw _validationFactory.Create("ContaBancariaId", "Conta bancária não encontrada.");
        }

        var formaPagamento = await _lookupCache.GetFormaPagamentoByIdAsync(conta.FormaPagamentoId, cancellationToken)
            ?? throw new InvalidOperationException("FormaPagamento not found");

        if ((formaPagamento.EhCartao || conta.CartaoId.HasValue) && !conta.FaturaCartaoId.HasValue)
        {
            throw _validationFactory.Create("CartaoId", "Compras em cartão devem ser liquidadas pela fatura.");
        }

        conta.Liquidar(request.DataLiquidacao, request.ContaBancariaId, StatusConta.LiquidadaId);

        if (conta.FaturaCartaoId.HasValue)
        {
            var fatura = await dbContext.FaturasCartao
                .SingleAsync(x => x.Id == conta.FaturaCartaoId.Value, cancellationToken);

            if (fatura.Status != StatusFaturaCartao.Paga)
            {
                fatura.Pagar(request.DataLiquidacao, request.ContaBancariaId, conta.Observacao);
            }

            var cartao = await dbContext.Cartoes
                .AsNoTracking()
                .SingleAsync(x => x.Id == fatura.CartaoId, cancellationToken);

            var itensDaFatura = await dbContext.ContasPagar
                .Where(x => x.CartaoId == fatura.CartaoId && x.StatusContaId != StatusConta.CanceladaId)
                .ToListAsync(cancellationToken);

            foreach (var itemFatura in itensDaFatura
                         .Where(x => FaturaCartaoCompetencia.Calcular(
                                 x.DataEmissao,
                                 cartao.DiaFechamentoFatura,
                                 cartao.DiaVencimentoFatura).Competencia == fatura.Competencia)
                         .Where(x => x.StatusContaId != StatusConta.LiquidadaId))
            {
                itemFatura.Liquidar(request.DataLiquidacao, request.ContaBancariaId, StatusConta.LiquidadaId);
            }
        }

        dbContext.MovimentacoesFinanceiras.Add(
            MovimentacaoFinanceira.CriarLiquidacaoContaPagar(
                conta.Id,
                request.ContaBancariaId,
                request.DataLiquidacao,
                conta.ValorLiquido,
                StatusMovimentacao.EfetivadaId,
                conta.Descricao,
                conta.FaturaCartaoId));

        await _eventDispatcher.DispatchAsync(
            new ContaPagarLiquidadaEvent(
                conta.Id,
                conta.ValorLiquido,
                request.DataLiquidacao,
                request.ContaBancariaId,
                conta.Descricao),
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return await _queryService.ObterPorIdAsync(conta.Id, cancellationToken);
    }

    public async Task<ContaPagarDetalheResponse?> EstornarAsync(Guid id, CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasPagar.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (conta is null)
        {
            return null;
        }

        try
        {
            conta.Estornar(StatusConta.PendenteId);
        }
        catch (InvalidOperationException exception)
        {
            throw ConverterParaValidacao(exception);
        }

        var movimentos = await dbContext.MovimentacoesFinanceiras
            .Where(x =>
                x.ContaPagarId == conta.Id &&
                x.Natureza == NaturezaMovimentacao.Realizada &&
                x.StatusMovimentacaoId != StatusMovimentacao.CanceladaId)
            .ToListAsync(cancellationToken);

        foreach (var movimento in movimentos)
        {
            movimento.Cancelar(StatusMovimentacao.CanceladaId);
        }

        if (conta.FaturaCartaoId.HasValue)
        {
            var fatura = await dbContext.FaturasCartao
                .SingleAsync(x => x.Id == conta.FaturaCartaoId.Value, cancellationToken);

            if (fatura.Status == StatusFaturaCartao.Paga)
            {
                fatura.ReabrirPagamento();
            }

            var cartao = await dbContext.Cartoes
                .AsNoTracking()
                .SingleAsync(x => x.Id == fatura.CartaoId, cancellationToken);

            var itensDaFatura = await dbContext.ContasPagar
                .Where(x => x.CartaoId == fatura.CartaoId && x.StatusContaId != StatusConta.CanceladaId)
                .ToListAsync(cancellationToken);

            foreach (var itemFatura in itensDaFatura
                         .Where(x => FaturaCartaoCompetencia.Calcular(
                                 x.DataEmissao,
                                 cartao.DiaFechamentoFatura,
                                 cartao.DiaVencimentoFatura).Competencia == fatura.Competencia)
                         .Where(x => x.StatusContaId == StatusConta.LiquidadaId))
            {
                // Compras de cartão voltam para "Em fatura": continuam pertencendo à fatura reaberta.
                itemFatura.Estornar(itemFatura.CartaoId.HasValue ? StatusConta.EmFaturaId : StatusConta.PendenteId);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await _queryService.ObterPorIdAsync(conta.Id, cancellationToken);
    }

    public async Task<ContaPagarDetalheResponse?> CancelarAsync(Guid id, CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasPagar.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (conta is null)
        {
            return null;
        }

        try
        {
            conta.Cancelar(StatusConta.CanceladaId);
        }
        catch (InvalidOperationException exception)
        {
            throw ConverterParaValidacao(exception);
        }

        var movimentoEconomico = await dbContext.MovimentacoesFinanceiras
            .SingleOrDefaultAsync(
                x => x.ContaPagarId == conta.Id && x.Natureza == NaturezaMovimentacao.Economica,
                cancellationToken);

        movimentoEconomico?.Cancelar(StatusMovimentacao.CanceladaId);

        await dbContext.SaveChangesAsync(cancellationToken);
        return await _queryService.ObterPorIdAsync(conta.Id, cancellationToken);
    }

    private async Task<PlanejamentoCompra?> ObterCompraPlanejadaOrigemAsync(Guid? origemCompraPlanejadaId, CancellationToken cancellationToken)
    {
        if (!origemCompraPlanejadaId.HasValue)
        {
            return null;
        }

        var compraPlanejada = await dbContext.ComprasPlanejadas
            .SingleOrDefaultAsync(x => x.Id == origemCompraPlanejadaId.Value, cancellationToken);

        if (compraPlanejada is null)
        {
            throw _validationFactory.Create("OrigemCompraPlanejadaId", "Compra planejada nao encontrada.");
        }

        if (compraPlanejada.ContaPagarGeradaId.HasValue)
        {
            throw _validationFactory.Create("OrigemCompraPlanejadaId", "Compra planejada ja foi convertida em conta a pagar.");
        }

        if (compraPlanejada.Status == StatusPlanejamentoCompra.Cancelada)
        {
            throw _validationFactory.Create("OrigemCompraPlanejadaId", "Compra planejada cancelada nao pode ser convertida.");
        }

        return compraPlanejada;
    }

    private async Task<ContaPagarValidationContext> ValidarCriacaoOuAtualizacaoAsync(
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
        {
            throw _validationFactory.Create("RecebedorId", "Recebedor não encontrado.");
        }

        if (responsavelCompraId.HasValue &&
            !await dbContext.Pessoas.AnyAsync(x => x.Id == responsavelCompraId.Value, cancellationToken))
        {
            throw _validationFactory.Create("ResponsavelCompraId", "Responsável não encontrado.");
        }

        var formaPagamento = await _lookupCache.GetFormaPagamentoByIdAsync(formaPagamentoId, cancellationToken);

        if (formaPagamento is null)
        {
            throw _validationFactory.Create("FormaPagamentoId", "Forma de pagamento não encontrada.");
        }

        if (quantidadeParcelas < 1)
        {
            throw _validationFactory.Create("QuantidadeParcelas", "Quantidade de parcelas deve ser maior que zero.");
        }

        Cartao? cartao = null;
        if (cartaoId.HasValue)
        {
            cartao = await dbContext.Cartoes
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == cartaoId.Value, cancellationToken);

            if (cartao is null)
            {
                throw _validationFactory.Create("CartaoId", "Cartão não encontrado.");
            }
        }

        if (contaBancariaId.HasValue &&
            !await dbContext.ContasBancarias.AnyAsync(x => x.Id == contaBancariaId.Value, cancellationToken))
        {
            throw _validationFactory.Create("ContaBancariaId", "Conta bancária não encontrada.");
        }

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
            {
                throw _validationFactory.Create("CartaoId", "Cartão é obrigatório para compras em cartão.");
            }

            if (contaBancariaId.HasValue)
            {
                throw _validationFactory.Create("ContaBancariaId", "Compras em cartão não geram saída bancária real neste momento.");
            }

            if (dataLiquidacao.HasValue)
            {
                throw _validationFactory.Create("DataLiquidacao", "Compras em cartão não devem informar data de liquidação.");
            }

            var competencia = FaturaCartaoCompetencia.Calcular(
                dataEmissao,
                cartao!.DiaFechamentoFatura,
                cartao.DiaVencimentoFatura);

            if (await dbContext.FaturasCartao.AnyAsync(
                    x => x.CartaoId == cartaoId.Value &&
                         x.Competencia == competencia.Competencia &&
                         x.Status == StatusFaturaCartao.Paga,
                    cancellationToken))
            {
                throw _validationFactory.Create("DataEmissao", "Já existe fatura paga para a competência desta compra em cartão.");
            }
        }
        else if (cartaoId.HasValue)
        {
            throw _validationFactory.Create("CartaoId", "Cartão informado para uma forma de pagamento que não é cartão.");
        }

        if (formaPagamento.BaixarAutomaticamente && !formaPagamento.EhCartao && !contaBancariaId.HasValue)
        {
            throw _validationFactory.Create("ContaBancariaId", "Conta bancária é obrigatória para baixa automática.");
        }

        if (!formaPagamento.BaixarAutomaticamente && !formaPagamento.EhCartao && dataLiquidacao.HasValue)
        {
            throw _validationFactory.Create("DataLiquidacao", "Data de liquidação só pode ser informada com baixa automática.");
        }

        return new ContaPagarValidationContext(
            formaPagamento.BaixarAutomaticamente && !formaPagamento.EhCartao,
            formaPagamento.EhCartao,
            cartao);
    }

    private IReadOnlyCollection<RateioPlano> ConverterRateios(IReadOnlyCollection<RateioRequest> rateios)
    {
        try
        {
            return rateios.Select(x => RateioPlano.Create(x.ContaGerencialId, x.Valor)).ToArray();
        }
        catch (ArgumentException exception)
        {
            throw _validationFactory.Create("Rateios", exception.Message);
        }
    }

    private static IReadOnlyCollection<MovimentacaoFinanceira> AplicarLiquidacaoAutomatica(
        IReadOnlyCollection<ContaPagar> contas,
        DateOnly? dataLiquidacao,
        Guid contaBancariaId)
    {
        var movimentos = new List<MovimentacaoFinanceira>(contas.Count);

        foreach (var conta in contas)
        {
            var dataMovimentacao = (dataLiquidacao ?? conta.DataEmissao).AddMonths(conta.NumeroParcela - 1);
            conta.Liquidar(dataMovimentacao, contaBancariaId, StatusConta.LiquidadaId);
            movimentos.Add(MovimentacaoFinanceira.CriarLiquidacaoContaPagar(
                conta.Id,
                contaBancariaId,
                dataMovimentacao,
                conta.ValorLiquido,
                StatusMovimentacao.EfetivadaId,
                conta.Descricao));
        }

        return movimentos;
    }

    private ApplicationValidationException ConverterParaValidacao(Exception exception)
    {
        return _validationFactory.Create("Request", exception.Message);
    }

    private TipoPeriodicidadeRecorrenciaDomain MapearTipoPeriodicidadeDominio(TipoPeriodicidadeRecorrenciaContract tipo)
    {
        return tipo switch
        {
            TipoPeriodicidadeRecorrenciaContract.Mensal => TipoPeriodicidadeRecorrenciaDomain.Mensal,
            _ => throw _validationFactory.Create("Recorrencia.TipoPeriodicidade", "Tipo de periodicidade de recorrência inválido.")
        };
    }

    private static TipoPeriodicidadeRecorrenciaContract MapearTipoPeriodicidadeContrato(TipoPeriodicidadeRecorrenciaDomain tipo)
    {
        return tipo switch
        {
            TipoPeriodicidadeRecorrenciaDomain.Mensal => TipoPeriodicidadeRecorrenciaContract.Mensal,
            _ => throw new ArgumentOutOfRangeException(nameof(tipo))
        };
    }

    private TipoDiaRecorrenciaDomain MapearTipoDiaDominio(TipoDiaRecorrenciaContract tipo)
    {
        return tipo switch
        {
            TipoDiaRecorrenciaContract.DiaFixo => TipoDiaRecorrenciaDomain.DiaFixo,
            TipoDiaRecorrenciaContract.DiaUtil => TipoDiaRecorrenciaDomain.DiaUtil,
            _ => throw _validationFactory.Create("Recorrencia.TipoDia", "Tipo de dia de recorrência inválido.")
        };
    }

    private static TipoDiaRecorrenciaContract MapearTipoDiaContrato(TipoDiaRecorrenciaDomain tipo)
    {
        return tipo switch
        {
            TipoDiaRecorrenciaDomain.DiaFixo => TipoDiaRecorrenciaContract.DiaFixo,
            TipoDiaRecorrenciaDomain.DiaUtil => TipoDiaRecorrenciaContract.DiaUtil,
            _ => throw new ArgumentOutOfRangeException(nameof(tipo))
        };
    }

    private void ValidarRecorrencia(
        DateOnly dataEmissao,
        RecorrenciaConfigRequest? recorrencia,
        int quantidadeParcelas)
    {
        if (recorrencia is not null && quantidadeParcelas != 1)
        {
            throw _validationFactory.Create("QuantidadeParcelas", "Recorrência inicial não pode ser combinada com parcelamento.");
        }

        if (recorrencia is null)
        {
            return;
        }

        var dataInicio = ResolveDataInicioRecorrencia(dataEmissao, recorrencia);
        var dataFim = ResolveDataFimRecorrencia(recorrencia);

        if (dataFim.HasValue && dataFim.Value < dataInicio)
        {
            throw _validationFactory.Create("Recorrencia.DataFim", "Data fim deve ser maior ou igual à primeira ocorrência da série.");
        }
    }

    private RegraRecorrencia CriarRegraRecorrencia(CriarContaPagarRequest request, RecorrenciaConfigRequest recorrencia)
    {
        var tipoDia = MapearTipoDiaDominio(recorrencia.TipoDia);
        var dataInicio = ResolveDataInicioRecorrencia(request.DataEmissao, recorrencia);
        var dataFim = ResolveDataFimRecorrencia(recorrencia);

        return RegraRecorrencia.Criar(
            TipoLancamentoRecorrencia.ContaPagar,
            MapearTipoPeriodicidadeDominio(recorrencia.TipoPeriodicidade),
            tipoDia,
            recorrencia.DiaOrdemMensal,
            dataInicio,
            dataFim,
            recorrencia.PermiteEdicaoOcorrenciaIndividual,
            recorrencia.Observacao,
            SerializarTemplate(request));
    }

    private DateOnly ResolveDataInicioRecorrencia(DateOnly dataEmissao, RecorrenciaConfigRequest recorrencia)
    {
        if (recorrencia.DataInicio.HasValue)
        {
            return RecorrenciaDateHelper.CalculateDateForReferenceMonth(
                recorrencia.DataInicio.Value,
                MapearTipoDiaDominio(recorrencia.TipoDia),
                recorrencia.DiaOrdemMensal);
        }

        return RecorrenciaDateHelper.CalculateAutomaticStartDate(
            dataEmissao,
            MapearTipoDiaDominio(recorrencia.TipoDia),
            recorrencia.DiaOrdemMensal);
    }

    private DateOnly? ResolveDataFimRecorrencia(RecorrenciaConfigRequest recorrencia)
    {
        if (!recorrencia.DataFim.HasValue)
        {
            return null;
        }

        return RecorrenciaDateHelper.CalculateDateForReferenceMonth(
            recorrencia.DataFim.Value,
            MapearTipoDiaDominio(recorrencia.TipoDia),
            recorrencia.DiaOrdemMensal);
    }

    private static string SerializarTemplate(CriarContaPagarRequest request)
    {
        return JsonSerializer.Serialize(new ContaPagarRecorrenciaTemplate(
            request.NumeroDocumento,
            request.DataEmissao,
            request.ResponsavelCompraId,
            request.RecebedorId,
            request.DataVencimento,
            request.FormaPagamentoId,
            request.CartaoId,
            request.ContaBancariaId,
            request.ValorOriginal,
            request.ValorDesconto,
            request.ValorJuros,
            request.ValorMulta,
            request.Descricao,
            request.Observacao,
            request.Rateios.Select(x => new RateioRecorrenciaTemplate(x.ContaGerencialId, x.Valor)).ToArray()));
    }

    private static string SerializarTemplate(AtualizarContaPagarRequest request)
    {
        return JsonSerializer.Serialize(new ContaPagarRecorrenciaTemplate(
            request.NumeroDocumento,
            request.DataEmissao,
            request.ResponsavelCompraId,
            request.RecebedorId,
            request.DataVencimento,
            request.FormaPagamentoId,
            request.CartaoId,
            request.ContaBancariaId,
            request.ValorOriginal,
            request.ValorDesconto,
            request.ValorJuros,
            request.ValorMulta,
            request.Descricao,
            request.Observacao,
            request.Rateios.Select(x => new RateioRecorrenciaTemplate(x.ContaGerencialId, x.Valor)).ToArray()));
    }

    private static ContaPagarRecorrenciaTemplate DesserializarTemplate(string templateJson)
    {
        return JsonSerializer.Deserialize<ContaPagarRecorrenciaTemplate>(templateJson)
               ?? throw new InvalidOperationException("Template de recorrência inválido.");
    }

    private static AtualizarContaPagarRequest AjustarRequestParaMes(AtualizarContaPagarRequest request, int monthOffset)
    {
        return request with
        {
            DataEmissao = RecorrenciaDateHelper.Shift(request.DataEmissao, monthOffset),
            DataVencimento = RecorrenciaDateHelper.Shift(request.DataVencimento, monthOffset)
        };
    }

    private static ContaPagar CriarOcorrenciaRecorrente(
        ContaPagarRecorrenciaTemplate template,
        Guid regraRecorrenciaId,
        DateOnly dataVencimento)
    {
        var monthOffset = RecorrenciaDateHelper.CalculateMonthOffset(template.DataVencimento, dataVencimento);

        return ContaPagar.Criar(
            template.NumeroDocumento,
            RecorrenciaDateHelper.Shift(template.DataEmissao, monthOffset),
            template.ResponsavelCompraId,
            template.RecebedorId,
            dataVencimento,
            template.FormaPagamentoId,
            template.CartaoId,
            template.ContaBancariaId,
            template.ValorOriginal,
            template.ValorDesconto,
            template.ValorJuros,
            template.ValorMulta,
            1,
            1,
            null,
            null,
            template.Descricao,
            template.Observacao,
            StatusConta.PendenteId,
            true,
            regraRecorrenciaId,
            OrigemLancamento.Recorrencia,
            template.Rateios.Select(x => RateioPlano.Create(x.ContaGerencialId, x.Valor)).ToArray());
    }

    private void AtualizarContaExistente(ContaPagar conta, AtualizarContaPagarRequest request)
    {
        try
        {
            conta.Atualizar(
                request.NumeroDocumento,
                request.DataEmissao,
                request.ResponsavelCompraId,
                request.RecebedorId,
                request.DataVencimento,
                request.FormaPagamentoId,
                request.CartaoId,
                request.ContaBancariaId,
                request.ValorOriginal,
                request.ValorDesconto,
                request.ValorJuros,
                request.ValorMulta,
                request.Descricao,
                request.Observacao,
                StatusConta.PendenteId,
                ConverterRateios(request.Rateios));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            throw ConverterParaValidacao(exception);
        }
    }

    private async Task SincronizarRateiosContaAsync(ContaPagar conta, CancellationToken cancellationToken)
    {
        var rateiosExistentes = await dbContext.RateiosContaGerencial
            .Where(x => x.ContaPagarId == conta.Id)
            .ToListAsync(cancellationToken);

        dbContext.RateiosContaGerencial.RemoveRange(rateiosExistentes);
        dbContext.RateiosContaGerencial.AddRange(conta.Rateios);
    }

    private async Task CancelarMovimentacaoEconomicaAsync(
        ContaPagar conta,
        CancellationToken cancellationToken)
    {
        var movimentoEconomico = await dbContext.MovimentacoesFinanceiras
            .SingleOrDefaultAsync(
                x => x.ContaPagarId == conta.Id && x.Natureza == NaturezaMovimentacao.Economica,
                cancellationToken);

        if (movimentoEconomico is not null)
        {
            movimentoEconomico.Cancelar(StatusMovimentacao.CanceladaId);
        }
    }

    private async Task<RegraRecorrencia> ObterRegraRecorrenciaObrigatoriaAsync(
        ContaPagar conta,
        CancellationToken cancellationToken)
    {
        if (!conta.RegraRecorrenciaId.HasValue)
        {
            throw _validationFactory.Create("Recorrencia", "A conta informada não possui regra de recorrência.");
        }

        return await dbContext.RegrasRecorrencia
            .SingleAsync(x => x.Id == conta.RegraRecorrenciaId.Value, cancellationToken);
    }

    private sealed record ContaPagarValidationContext(bool LiquidarNaCriacao, bool CompraCartao, Cartao? Cartao);

    private sealed record ContaPagarRecorrenciaTemplate(
        string? NumeroDocumento,
        DateOnly DataEmissao,
        Guid? ResponsavelCompraId,
        Guid RecebedorId,
        DateOnly DataVencimento,
        Guid FormaPagamentoId,
        Guid? CartaoId,
        Guid? ContaBancariaId,
        decimal ValorOriginal,
        decimal ValorDesconto,
        decimal ValorJuros,
        decimal ValorMulta,
        string Descricao,
        string? Observacao,
        IReadOnlyCollection<RateioRecorrenciaTemplate> Rateios);

    private sealed record RateioRecorrenciaTemplate(Guid ContaGerencialId, decimal Valor);
}
