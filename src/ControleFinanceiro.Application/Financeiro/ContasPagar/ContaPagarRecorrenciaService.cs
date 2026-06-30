using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Financeiro.Recorrencias;
using ControleFinanceiro.Contracts.Financeiro.Common;
using ControleFinanceiro.Contracts.Financeiro.ContasPagar;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Financeiro.ContasPagar;

public interface IContaPagarRecorrenciaService
{
    Task<ContaPagarDetalheResponse?> AtualizarAsync(Guid id, AtualizarContaPagarRequest request, CancellationToken cancellationToken);
    Task<ContaPagarDetalheResponse?> AlterarFuturasAsync(Guid id, AtualizarContaPagarRequest request, CancellationToken cancellationToken);
    Task<ContaPagarDetalheResponse?> GerarOcorrenciasAsync(Guid id, GerarOcorrenciasRecorrenciaRequest request, CancellationToken cancellationToken);
    Task<ContaPagarDetalheResponse?> PausarRecorrenciaAsync(Guid id, CancellationToken cancellationToken);
    Task<ContaPagarDetalheResponse?> EncerrarRecorrenciaAsync(Guid id, EncerrarRecorrenciaRequest request, CancellationToken cancellationToken);
}

public sealed class ContaPagarRecorrenciaService(
    IAppDbContext dbContext,
    IContaPagarQueryService queryService,
    ContaPagarSharedHelper helper) : IContaPagarRecorrenciaService
{
    public async Task<ContaPagarDetalheResponse?> AtualizarAsync(Guid id, AtualizarContaPagarRequest request, CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasPagar.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (conta is null) return null;

        if (request.QuantidadeParcelas != conta.QuantidadeParcelas)
            throw helper.CriarErroValidacao("QuantidadeParcelas", "Não é permitido alterar o parcelamento na edição.");

        helper.ValidarRecorrencia(request.DataEmissao, request.Recorrencia, request.QuantidadeParcelas);

        Guid? regraRecorrenciaCriadaId = null;
        if (!conta.RegraRecorrenciaId.HasValue && request.Recorrencia is not null)
        {
            var novaRegra = helper.CriarRegraRecorrencia(request, request.Recorrencia);
            dbContext.RegrasRecorrencia.Add(novaRegra);
            regraRecorrenciaCriadaId = novaRegra.Id;
        }

        if (conta.RegraRecorrenciaId.HasValue)
        {
            var regra = await dbContext.RegrasRecorrencia.SingleAsync(x => x.Id == conta.RegraRecorrenciaId.Value, cancellationToken);

            if (!regra.PermiteEdicaoOcorrenciaIndividual)
                throw helper.CriarErroValidacao("Recorrencia", "A regra atual não permite edição pontual da ocorrência.");

            if (request.Recorrencia is not null)
            {
                regra.Atualizar(
                    helper.MapearTipoPeriodicidadeDominio(request.Recorrencia.TipoPeriodicidade),
                    helper.MapearTipoDiaDominio(request.Recorrencia.TipoDia),
                    request.Recorrencia.DiaOrdemMensal,
                    helper.ResolveDataInicioRecorrencia(request.DataEmissao, request.Recorrencia),
                    helper.ResolveDataFimRecorrencia(request.Recorrencia),
                    request.Recorrencia.PermiteEdicaoOcorrenciaIndividual,
                    request.Recorrencia.Observacao,
                    ContaPagarSharedHelper.SerializarTemplate(request));
            }
        }

        var contexto = await helper.ValidarCriacaoOuAtualizacaoAsync(
            request.DataEmissao, request.RecebedorId, request.ResponsavelCompraId,
            request.FormaPagamentoId, request.CartaoId, request.ContaBancariaId,
            request.DataLiquidacao, request.QuantidadeParcelas, request.Rateios, cancellationToken);

        helper.AtualizarContaExistente(conta, request);
        if (regraRecorrenciaCriadaId.HasValue) conta.VincularRecorrencia(regraRecorrenciaCriadaId.Value);

        await helper.SincronizarRateiosContaAsync(conta, cancellationToken);

        if (contexto.LiquidarNaCriacao &&
            !await dbContext.MovimentacoesFinanceiras.AnyAsync(x => x.ContaPagarId == conta.Id, cancellationToken))
        {
            dbContext.MovimentacoesFinanceiras.AddRange(
                ContaPagarSharedHelper.AplicarLiquidacaoAutomatica([conta], request.DataLiquidacao, request.ContaBancariaId!.Value));
        }

        await helper.CancelarMovimentacaoEconomicaAsync(conta, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await queryService.ObterPorIdAsync(conta.Id, cancellationToken);
    }

    public async Task<ContaPagarDetalheResponse?> AlterarFuturasAsync(Guid id, AtualizarContaPagarRequest request, CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasPagar.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (conta is null) return null;

        var regra = await helper.ObterRegraRecorrenciaObrigatoriaAsync(conta, cancellationToken);
        helper.ValidarRecorrencia(request.DataEmissao, request.Recorrencia, request.QuantidadeParcelas);

        var recorrencia = request.Recorrencia ?? new RecorrenciaConfigRequest(
            ContaPagarSharedHelper.MapearTipoPeriodicidadeContrato(regra.TipoPeriodicidade),
            ContaPagarSharedHelper.MapearTipoDiaContrato(regra.TipoDia),
            regra.DiaOrdemMensal, regra.DataInicio, regra.DataFim,
            regra.PermiteEdicaoOcorrenciaIndividual, regra.Observacao);

        await helper.ValidarCriacaoOuAtualizacaoAsync(
            request.DataEmissao, request.RecebedorId, request.ResponsavelCompraId,
            request.FormaPagamentoId, request.CartaoId, request.ContaBancariaId,
            request.DataLiquidacao, request.QuantidadeParcelas, request.Rateios, cancellationToken);

        regra.Atualizar(
            helper.MapearTipoPeriodicidadeDominio(recorrencia.TipoPeriodicidade),
            helper.MapearTipoDiaDominio(recorrencia.TipoDia),
            recorrencia.DiaOrdemMensal,
            helper.ResolveDataInicioRecorrencia(request.DataEmissao, recorrencia),
            helper.ResolveDataFimRecorrencia(recorrencia),
            recorrencia.PermiteEdicaoOcorrenciaIndividual,
            recorrencia.Observacao,
            ContaPagarSharedHelper.SerializarTemplate(request));

        var contasFuturas = await dbContext.ContasPagar
            .Where(x => x.RegraRecorrenciaId == regra.Id &&
                        x.DataVencimento >= conta.DataVencimento &&
                        x.StatusContaId != StatusConta.LiquidadaId &&
                        x.StatusContaId != StatusConta.CanceladaId)
            .OrderBy(x => x.DataVencimento)
            .ToListAsync(cancellationToken);

        foreach (var contaFutura in contasFuturas)
        {
            var mesOffset = RecorrenciaDateHelper.CalculateMonthOffset(conta.DataVencimento, contaFutura.DataVencimento);
            var requestAjustado = ContaPagarSharedHelper.AjustarRequestParaMes(request, mesOffset);
            helper.AtualizarContaExistente(contaFutura, requestAjustado);
            await helper.SincronizarRateiosContaAsync(contaFutura, cancellationToken);
            await helper.ValidarCriacaoOuAtualizacaoAsync(
                requestAjustado.DataEmissao, requestAjustado.RecebedorId, requestAjustado.ResponsavelCompraId,
                requestAjustado.FormaPagamentoId, requestAjustado.CartaoId, requestAjustado.ContaBancariaId,
                requestAjustado.DataLiquidacao, requestAjustado.QuantidadeParcelas, requestAjustado.Rateios, cancellationToken);
            await helper.CancelarMovimentacaoEconomicaAsync(contaFutura, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await queryService.ObterPorIdAsync(conta.Id, cancellationToken);
    }

    public async Task<ContaPagarDetalheResponse?> GerarOcorrenciasAsync(Guid id, GerarOcorrenciasRecorrenciaRequest request, CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasPagar.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (conta is null) return null;

        var regra = await helper.ObterRegraRecorrenciaObrigatoriaAsync(conta, cancellationToken);

        if (!regra.Ativa)
            throw helper.CriarErroValidacao("Recorrencia", "A recorrência está pausada ou encerrada.");

        var datasExistentes = await dbContext.ContasPagar
            .Where(x => x.RegraRecorrenciaId == regra.Id)
            .Select(x => x.DataVencimento)
            .ToArrayAsync(cancellationToken);

        var datasPendentes = regra.CalcularDatasPendentes(datasExistentes, request.AteData);
        var template = ContaPagarSharedHelper.DesserializarTemplate(regra.TemplateJson);

        var novasContas = datasPendentes
            .Select(dataVencimento => ContaPagarSharedHelper.CriarOcorrenciaRecorrente(template, regra.Id, dataVencimento))
            .ToArray();

        dbContext.ContasPagar.AddRange(novasContas);
        dbContext.RateiosContaGerencial.AddRange(novasContas.SelectMany(x => x.Rateios));

        await dbContext.SaveChangesAsync(cancellationToken);
        return await queryService.ObterPorIdAsync(conta.Id, cancellationToken);
    }

    public async Task<ContaPagarDetalheResponse?> PausarRecorrenciaAsync(Guid id, CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasPagar.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (conta is null) return null;

        var regra = await helper.ObterRegraRecorrenciaObrigatoriaAsync(conta, cancellationToken);
        regra.Pausar();

        await dbContext.SaveChangesAsync(cancellationToken);
        return await queryService.ObterPorIdAsync(conta.Id, cancellationToken);
    }

    public async Task<ContaPagarDetalheResponse?> EncerrarRecorrenciaAsync(Guid id, EncerrarRecorrenciaRequest request, CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasPagar.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (conta is null) return null;

        var regra = await helper.ObterRegraRecorrenciaObrigatoriaAsync(conta, cancellationToken);
        regra.Encerrar(request.DataFim);

        var contasPosteriores = await dbContext.ContasPagar
            .Where(x => x.RegraRecorrenciaId == regra.Id &&
                        x.DataVencimento > request.DataFim &&
                        x.StatusContaId != StatusConta.LiquidadaId &&
                        x.StatusContaId != StatusConta.CanceladaId)
            .ToListAsync(cancellationToken);

        foreach (var contaPosterior in contasPosteriores)
        {
            contaPosterior.Cancelar(StatusConta.CanceladaId);
            var movimentoEconomico = await dbContext.MovimentacoesFinanceiras
                .SingleOrDefaultAsync(x => x.ContaPagarId == contaPosterior.Id && x.Natureza == NaturezaMovimentacao.Economica, cancellationToken);
            movimentoEconomico?.Cancelar(StatusMovimentacao.CanceladaId);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await queryService.ObterPorIdAsync(conta.Id, cancellationToken);
    }
}
