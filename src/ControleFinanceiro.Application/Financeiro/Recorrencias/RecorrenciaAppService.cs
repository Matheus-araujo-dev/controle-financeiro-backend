using ControleFinanceiro.Application.Financeiro.ContasPagar;
using ControleFinanceiro.Application.Financeiro.ContasReceber;
using ControleFinanceiro.Application.Common.Extensions;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Contracts.Financeiro.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DomainOrigemLancamento = ControleFinanceiro.Domain.Financeiro.OrigemLancamento;

namespace ControleFinanceiro.Application.Financeiro.Recorrencias;

public sealed class RecorrenciaAppService(
    IAppDbContext dbContext,
    ContaPagarAppService contaPagarAppService,
    ContaReceberAppService contaReceberAppService,
    ILogger<RecorrenciaAppService> logger)
{
    public async Task GerarOcorrenciasRecorrentesNoMesAsync(DateOnly dataReferencia, CancellationToken cancellationToken)
    {
        logger.LogInformation("Iniciando geração automática de recorrências para o mês {Mes}/{Ano}.", dataReferencia.Month, dataReferencia.Year);
        
        var ateData = new DateOnly(dataReferencia.Year, dataReferencia.Month, DateTime.DaysInMonth(dataReferencia.Year, dataReferencia.Month));
        var request = new GerarOcorrenciasRecorrenciaRequest(ateData);

        // 1. Obter todas as regras ativas (worker roda sem tenant: filtro global desativado)
        var regrasAtivas = await dbContext.RegrasRecorrencia
            .Where(x => x.Ativa)
            .Select(x => new { x.Id, x.TipoLancamento, x.FamiliaId })
            .ToArrayAsync(cancellationToken);

        int totalGerado = 0;

        foreach (var regra in regrasAtivas)
        {
            try
            {
                // Garante que consultas e novas entidades fiquem na família dona da regra.
                if (regra.FamiliaId != Guid.Empty)
                {
                    dbContext.DefinirFamiliaCorrente(regra.FamiliaId);
                }

                if (regra.TipoLancamento == Domain.Financeiro.TipoLancamentoRecorrencia.ContaPagar)
                {
                    await contaPagarAppService.GerarOcorrenciasAsync(regra.Id, request, cancellationToken);
                }
                else
                {
                    await contaReceberAppService.GerarOcorrenciasAsync(regra.Id, request, cancellationToken);
                }
                totalGerado++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro ao gerar ocorrência para a regra {RegraId}.", regra.Id);
            }
        }

        logger.LogInformation("Geração automática concluída. Total de regras processadas: {Total}.", totalGerado);
    }

    public async Task<RecorrenciaListResponse> ListarAtivasAsync(CancellationToken cancellationToken)
    {
        var regras = await dbContext.RegrasRecorrencia
            .AsNoTracking()
            .Where(x => x.Ativa)
            .OrderBy(x => x.DataInicio)
            .ToListAsync(cancellationToken);

        if (regras.Count == 0)
        {
            return new RecorrenciaListResponse([], new RecorrenciaListSummaryResponse(0, 0m));
        }

        var regraIds = regras.Select(x => x.Id).ToArray();

        var contasPagarOrigem = await (
            from conta in dbContext.ContasPagar.AsNoTracking()
            join recebedor in dbContext.Pessoas.AsNoTracking() on conta.RecebedorId equals recebedor.Id
            join responsavel in dbContext.Pessoas.AsNoTracking() on conta.ResponsavelCompraId equals responsavel.Id into responsaveis
            from responsavel in responsaveis.DefaultIfEmpty()
            where conta.RegraRecorrenciaId.HasValue &&
                  conta.Origem != DomainOrigemLancamento.Recorrencia
            where regraIds.Contains(conta.RegraRecorrenciaId!.Value)
            orderby conta.CreatedAtUtc
            select new ContaOrigemProjection(
                conta.RegraRecorrenciaId!.Value,
                "ContaPagar",
                conta.Id,
                conta.Descricao,
                conta.ValorLiquido,
                recebedor.Nome,
                responsavel != null ? responsavel.Nome : null))
            .ToListAsync(cancellationToken);

        var contasReceberOrigem = await (
            from conta in dbContext.ContasReceber.AsNoTracking()
            join pagador in dbContext.Pessoas.AsNoTracking() on conta.PagadorId equals pagador.Id
            join responsavel in dbContext.Pessoas.AsNoTracking() on conta.ResponsavelId equals responsavel.Id into responsaveis
            from responsavel in responsaveis.DefaultIfEmpty()
            where conta.RegraRecorrenciaId.HasValue &&
                  conta.Origem != DomainOrigemLancamento.Recorrencia
            where regraIds.Contains(conta.RegraRecorrenciaId!.Value)
            orderby conta.CreatedAtUtc
            select new ContaOrigemProjection(
                conta.RegraRecorrenciaId!.Value,
                "ContaReceber",
                conta.Id,
                conta.Descricao,
                conta.ValorLiquido,
                pagador.Nome,
                responsavel != null ? responsavel.Nome : null))
            .ToListAsync(cancellationToken);

        var contaOrigemLookup = contasPagarOrigem
            .Concat(contasReceberOrigem)
            .GroupBy(x => x.RegraRecorrenciaId)
            .ToDictionary(group => group.Key, group => group.First());

        var items = regras
            .Where(regra => contaOrigemLookup.ContainsKey(regra.Id))
            .Select(regra => MapearRecorrenciaListItem(regra, contaOrigemLookup[regra.Id]))
            .ToArray();

        return new RecorrenciaListResponse(
            items,
            new RecorrenciaListSummaryResponse(
                items.Length,
                decimal.Round(items.Sum(item => item.ValorLiquido), 2, MidpointRounding.AwayFromZero)));
    }

    private static RecorrenciaResponse MapearRecorrencia(Domain.Financeiro.RegraRecorrencia regra)
    {
        return new RecorrenciaResponse(
            regra.Id,
            (TipoPeriodicidadeRecorrencia)regra.TipoPeriodicidade,
            (TipoDiaRecorrencia)regra.TipoDia,
            regra.DiaOrdemMensal,
            regra.DataInicio,
            regra.DataFim,
            regra.Ativa,
            regra.PermiteEdicaoOcorrenciaIndividual,
            regra.Observacao);
    }

    private static RecorrenciaListItemResponse MapearRecorrenciaListItem(
        Domain.Financeiro.RegraRecorrencia regra,
        ContaOrigemProjection origem)
    {
        return new RecorrenciaListItemResponse(
            regra.Id,
            (TipoPeriodicidadeRecorrencia)regra.TipoPeriodicidade,
            (TipoDiaRecorrencia)regra.TipoDia,
            regra.DiaOrdemMensal,
            regra.DataInicio,
            regra.DataFim,
            regra.Ativa,
            regra.PermiteEdicaoOcorrenciaIndividual,
            regra.Observacao,
            origem.ContaOrigemTipo,
            origem.ContaOrigemId,
            origem.Descricao,
            origem.ValorLiquido,
            origem.PessoaNome,
            origem.ResponsavelNome);
    }

    private sealed record ContaOrigemProjection(
        Guid RegraRecorrenciaId,
        string ContaOrigemTipo,
        Guid ContaOrigemId,
        string Descricao,
        decimal ValorLiquido,
        string PessoaNome,
        string? ResponsavelNome);
}
