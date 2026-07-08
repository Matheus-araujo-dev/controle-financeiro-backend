using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Application.Financeiro.Recorrencias;
using ControleFinanceiro.Contracts.Financeiro.Common;
using ControleFinanceiro.Contracts.Financeiro.ContasReceber;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Financeiro.ContasReceber;

public interface IContaReceberRecorrenciaService
{
    Task<ContaReceberDetalheResponse?> AtualizarAsync(Guid id, AtualizarContaReceberRequest request, CancellationToken cancellationToken);
    Task<ContaReceberDetalheResponse?> AlterarFuturasAsync(Guid id, AtualizarContaReceberRequest request, CancellationToken cancellationToken);
    Task<ContaReceberDetalheResponse?> GerarOcorrenciasAsync(Guid id, GerarOcorrenciasRecorrenciaRequest request, CancellationToken cancellationToken);
    Task GerarPorRegraAsync(RegraRecorrencia regra, DateOnly ateData, CancellationToken cancellationToken);
    Task CancelarFuturasNaoPagasAsync(Guid regraId, DateOnly aPartirDe, CancellationToken cancellationToken);
    Task<ContaReceberDetalheResponse?> PausarRecorrenciaAsync(Guid id, CancellationToken cancellationToken);
    Task<ContaReceberDetalheResponse?> EncerrarRecorrenciaAsync(Guid id, EncerrarRecorrenciaRequest request, CancellationToken cancellationToken);
}

public sealed class ContaReceberRecorrenciaService(
    IAppDbContext dbContext,
    IContaReceberQueryService queryService,
    ContaReceberSharedHelper helper) : IContaReceberRecorrenciaService
{
    public async Task<ContaReceberDetalheResponse?> AtualizarAsync(
        Guid id, AtualizarContaReceberRequest request, CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasReceber.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (conta is null) return null;

        if (request.QuantidadeParcelas != conta.QuantidadeParcelas)
            throw ValidationExceptionFactory.Create("QuantidadeParcelas", "Não é permitido alterar o parcelamento na edição.");

        ContaReceberSharedHelper.ValidarRecorrencia(request.DataEmissao, request.Recorrencia, request.QuantidadeParcelas);

        Guid? regraRecorrenciaCriadaId = null;
        if (!conta.RegraRecorrenciaId.HasValue && request.Recorrencia is not null)
        {
            var novaRegra = ContaReceberSharedHelper.CriarRegraRecorrencia(request, request.Recorrencia);
            dbContext.RegrasRecorrencia.Add(novaRegra);
            regraRecorrenciaCriadaId = novaRegra.Id;
        }

        if (conta.RegraRecorrenciaId.HasValue)
        {
            var regra = await dbContext.RegrasRecorrencia.SingleAsync(x => x.Id == conta.RegraRecorrenciaId.Value, cancellationToken);

            if (!regra.PermiteEdicaoOcorrenciaIndividual)
                throw ValidationExceptionFactory.Create("Recorrencia", "A regra atual não permite edição pontual da ocorrência.");

            if (request.Recorrencia is not null)
            {
                regra.Atualizar(
                    ContaReceberSharedHelper.MapearTipoPeriodicidadeDominio(request.Recorrencia.TipoPeriodicidade),
                    ContaReceberSharedHelper.MapearTipoDiaDominio(request.Recorrencia.TipoDia),
                    request.Recorrencia.DiaOrdemMensal,
                    ContaReceberSharedHelper.ResolveDataInicioRecorrencia(request.DataEmissao, request.Recorrencia),
                    ContaReceberSharedHelper.ResolveDataFimRecorrencia(request.Recorrencia),
                    request.Recorrencia.PermiteEdicaoOcorrenciaIndividual,
                    request.Recorrencia.Observacao,
                    ContaReceberSharedHelper.SerializarTemplate(request));
            }
        }

        var liquidarNaCriacao = await helper.ValidarCriacaoOuAtualizacaoAsync(
            request.PagadorId, request.ResponsavelId, request.FormaPagamentoId,
            request.CartaoId, request.ContaBancariaId, request.DataLiquidacao,
            request.QuantidadeParcelas, request.Rateios, cancellationToken);

        ContaReceberSharedHelper.AtualizarContaExistente(conta, request);
        if (regraRecorrenciaCriadaId.HasValue) conta.VincularRecorrencia(regraRecorrenciaCriadaId.Value);

        await helper.SincronizarRateiosContaAsync(conta, cancellationToken);

        if (liquidarNaCriacao &&
            !await dbContext.MovimentacoesFinanceiras.AnyAsync(x => x.ContaReceberId == conta.Id, cancellationToken))
        {
            dbContext.MovimentacoesFinanceiras.AddRange(
                ContaReceberSharedHelper.AplicarLiquidacaoAutomatica([conta], request.DataLiquidacao, request.ContaBancariaId!.Value));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await queryService.ObterPorIdAsync(conta.Id, cancellationToken);
    }

    public async Task<ContaReceberDetalheResponse?> AlterarFuturasAsync(
        Guid id, AtualizarContaReceberRequest request, CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasReceber.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (conta is null) return null;

        var regra = await helper.ObterRegraRecorrenciaObrigatoriaAsync(conta, cancellationToken);
        ContaReceberSharedHelper.ValidarRecorrencia(request.DataEmissao, request.Recorrencia, request.QuantidadeParcelas);

        var recorrencia = request.Recorrencia ?? new RecorrenciaConfigRequest(
            ContaReceberSharedHelper.MapearTipoPeriodicidadeContrato(regra.TipoPeriodicidade),
            ContaReceberSharedHelper.MapearTipoDiaContrato(regra.TipoDia),
            regra.DiaOrdemMensal, regra.DataInicio, regra.DataFim,
            regra.PermiteEdicaoOcorrenciaIndividual, regra.Observacao);

        await helper.ValidarCriacaoOuAtualizacaoAsync(
            request.PagadorId, request.ResponsavelId, request.FormaPagamentoId,
            request.CartaoId, request.ContaBancariaId, request.DataLiquidacao,
            request.QuantidadeParcelas, request.Rateios, cancellationToken);

        regra.Atualizar(
            ContaReceberSharedHelper.MapearTipoPeriodicidadeDominio(recorrencia.TipoPeriodicidade),
            ContaReceberSharedHelper.MapearTipoDiaDominio(recorrencia.TipoDia),
            recorrencia.DiaOrdemMensal,
            ContaReceberSharedHelper.ResolveDataInicioRecorrencia(request.DataEmissao, recorrencia),
            ContaReceberSharedHelper.ResolveDataFimRecorrencia(recorrencia),
            recorrencia.PermiteEdicaoOcorrenciaIndividual,
            recorrencia.Observacao,
            ContaReceberSharedHelper.SerializarTemplate(request));

        var contasFuturas = await dbContext.ContasReceber
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
            var requestAjustado = ContaReceberSharedHelper.AjustarRequestParaMes(request, mesOffset);
            ContaReceberSharedHelper.AtualizarContaExistente(contaFutura, requestAjustado);
            await helper.SincronizarRateiosContaAsync(contaFutura, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await queryService.ObterPorIdAsync(conta.Id, cancellationToken);
    }

    public async Task<ContaReceberDetalheResponse?> GerarOcorrenciasAsync(
        Guid id, GerarOcorrenciasRecorrenciaRequest request, CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasReceber.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (conta is null) return null;

        var regra = await helper.ObterRegraRecorrenciaObrigatoriaAsync(conta, cancellationToken);

        if (!regra.Ativa)
            throw ValidationExceptionFactory.Create("Recorrencia", "A recorrência está pausada ou encerrada.");

        var datasExistentes = await dbContext.ContasReceber
            .Where(x => x.RegraRecorrenciaId == regra.Id)
            .Select(x => x.DataVencimento)
            .ToArrayAsync(cancellationToken);

        var datasPendentes = regra.CalcularDatasPendentes(datasExistentes, request.AteData);
        var template = ContaReceberSharedHelper.DesserializarTemplate(regra.TemplateJson);

        var novasContas = datasPendentes
            .Select(dataVencimento => ContaReceberSharedHelper.CriarOcorrenciaRecorrente(template, regra.Id, dataVencimento))
            .ToArray();

        dbContext.ContasReceber.AddRange(novasContas);
        dbContext.RateiosContaGerencial.AddRange(novasContas.SelectMany(x => x.Rateios));

        await dbContext.SaveChangesAsync(cancellationToken);
        return await queryService.ObterPorIdAsync(conta.Id, cancellationToken);
    }

    public async Task GerarPorRegraAsync(RegraRecorrencia regra, DateOnly ateData, CancellationToken cancellationToken)
    {
        var datasExistentes = await dbContext.ContasReceber
            .Where(x => x.RegraRecorrenciaId == regra.Id)
            .Select(x => x.DataVencimento)
            .ToArrayAsync(cancellationToken);

        var datasPendentes = regra.CalcularDatasPendentes(datasExistentes, ateData);
        if (datasPendentes.Count == 0) return;

        var template = ContaReceberSharedHelper.DesserializarTemplate(regra.TemplateJson);
        var novasContas = datasPendentes
            .Select(dv => ContaReceberSharedHelper.CriarOcorrenciaRecorrente(template, regra.Id, dv))
            .ToArray();

        dbContext.ContasReceber.AddRange(novasContas);
        dbContext.RateiosContaGerencial.AddRange(novasContas.SelectMany(x => x.Rateios));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelarFuturasNaoPagasAsync(Guid regraId, DateOnly aPartirDe, CancellationToken cancellationToken)
    {
        var contasFuturas = await dbContext.ContasReceber
            .Where(x => x.RegraRecorrenciaId == regraId &&
                        x.DataVencimento >= aPartirDe &&
                        x.StatusContaId != StatusConta.LiquidadaId &&
                        x.StatusContaId != StatusConta.CanceladaId)
            .ToListAsync(cancellationToken);

        foreach (var conta in contasFuturas)
            conta.Cancelar(StatusConta.CanceladaId);
    }

    public async Task<ContaReceberDetalheResponse?> PausarRecorrenciaAsync(Guid id, CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasReceber.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (conta is null) return null;

        var regra = await helper.ObterRegraRecorrenciaObrigatoriaAsync(conta, cancellationToken);
        regra.Pausar();

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        await CancelarFuturasNaoPagasAsync(regra.Id, hoje, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return await queryService.ObterPorIdAsync(conta.Id, cancellationToken);
    }

    public async Task<ContaReceberDetalheResponse?> EncerrarRecorrenciaAsync(
        Guid id, EncerrarRecorrenciaRequest request, CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasReceber.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (conta is null) return null;

        var regra = await helper.ObterRegraRecorrenciaObrigatoriaAsync(conta, cancellationToken);
        regra.Encerrar(request.DataFim);

        var contasPosteriores = await dbContext.ContasReceber
            .Where(x =>
                x.RegraRecorrenciaId == regra.Id &&
                x.DataVencimento > request.DataFim &&
                x.StatusContaId != StatusConta.LiquidadaId &&
                x.StatusContaId != StatusConta.CanceladaId)
            .ToListAsync(cancellationToken);

        foreach (var contaPosterior in contasPosteriores)
            contaPosterior.Cancelar(StatusConta.CanceladaId);

        await dbContext.SaveChangesAsync(cancellationToken);
        return await queryService.ObterPorIdAsync(conta.Id, cancellationToken);
    }
}
