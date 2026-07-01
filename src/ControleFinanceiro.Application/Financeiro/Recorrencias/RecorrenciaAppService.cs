using ControleFinanceiro.Application.Financeiro.ContasPagar;
using ControleFinanceiro.Application.Financeiro.ContasReceber;
using ControleFinanceiro.Application.Common.Extensions;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Contracts.Financeiro.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DomainOrigemLancamento = ControleFinanceiro.Domain.Financeiro.OrigemLancamento;
using DomainTipoPeriodicidade = ControleFinanceiro.Domain.Financeiro.TipoPeriodicidadeRecorrencia;
using DomainTipoDia = ControleFinanceiro.Domain.Financeiro.TipoDiaRecorrencia;

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

    public async Task<RecorrenciaListResponse> ListarAsync(RecorrenciaListQueryRequest query, CancellationToken cancellationToken)
    {
        // Cada item é uma regra + sua conta de origem (Origem != Recorrencia, a conta original que
        // criou a regra). Como o tipo da regra define a tabela, origens de pagar/receber nunca colidem.
        // Os filtros e o dedup (origem mais antiga por regra) rodam no banco — não materializa mais as
        // tabelas inteiras. O EF Core não traduz UNION com projeção para tipo custom, então a fusão
        // pagar+receber, ordenação e paginação ocorrem sobre o conjunto JÁ FILTRADO (recorrências são
        // de baixo volume, tornando a etapa final em memória barata).
        var tipo = query.Tipo?.Trim();
        var incluiPagar = !string.Equals(tipo, "Receber", StringComparison.OrdinalIgnoreCase);
        var incluiReceber = !string.Equals(tipo, "Pagar", StringComparison.OrdinalIgnoreCase);
        var termo = string.IsNullOrWhiteSpace(query.Search) ? null : $"%{query.Search.Trim().ToLower()}%";

        var rows = new List<RecorrenciaRow>();
        if (incluiPagar)
        {
            rows.AddRange(await ConsultarOrigensPagarAsync(query, termo, cancellationToken));
        }

        if (incluiReceber)
        {
            rows.AddRange(await ConsultarOrigensReceberAsync(query, termo, cancellationToken));
        }

        var ordenadas = ((query.SortBy ?? string.Empty).ToLowerInvariant() switch
        {
            "descricao" => Ordenar(rows, query, x => x.Descricao),
            "pessoanome" => Ordenar(rows, query, x => x.PessoaNome),
            "valorliquido" => Ordenar(rows, query, x => x.ValorLiquido),
            "diaordemmensal" => Ordenar(rows, query, x => x.DiaOrdemMensal),
            "datainicio" => Ordenar(rows, query, x => x.DataInicio),
            "ativa" => Ordenar(rows, query, x => x.Ativa),
            _ => Ordenar(rows, query, x => x.DataInicio)
        }).ToArray();

        var totalItems = ordenadas.Length;
        var valorTotal = ordenadas.Sum(r => r.ValorLiquido);
        var page = query.NormalizedPage;
        var pageSize = query.NormalizedPageSize;
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);

        var items = ordenadas
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(MapearRow)
            .ToArray();

        return new RecorrenciaListResponse(
            items,
            page,
            pageSize,
            totalItems,
            totalPages,
            new RecorrenciaListSummaryResponse(
                totalItems,
                decimal.Round(valorTotal, 2, MidpointRounding.AwayFromZero)));
    }

    public Task<RecorrenciaListResponse> ListarAtivasAsync(CancellationToken cancellationToken) =>
        ListarAsync(new RecorrenciaListQueryRequest { Ativa = true }, cancellationToken);

    private Task<List<RecorrenciaRow>> ConsultarOrigensPagarAsync(
        RecorrenciaListQueryRequest query, string? termo, CancellationToken cancellationToken)
    {
        var ativa = query.Ativa;
        var dataInicial = query.DataReferenciaInicial;
        var dataFinal = query.DataReferenciaFinal;

        var consulta =
            from conta in dbContext.ContasPagar.AsNoTracking()
            where conta.RegraRecorrenciaId.HasValue
                  && conta.Origem != DomainOrigemLancamento.Recorrencia
                  && !dbContext.ContasPagar.Any(outra =>
                      outra.RegraRecorrenciaId == conta.RegraRecorrenciaId
                      && outra.Origem != DomainOrigemLancamento.Recorrencia
                      && outra.CreatedAtUtc < conta.CreatedAtUtc)
            join regra in dbContext.RegrasRecorrencia.AsNoTracking() on conta.RegraRecorrenciaId!.Value equals regra.Id
            join recebedor in dbContext.Pessoas.AsNoTracking() on conta.RecebedorId equals recebedor.Id
            join responsavel in dbContext.Pessoas.AsNoTracking() on conta.ResponsavelCompraId equals responsavel.Id into responsaveis
            from responsavel in responsaveis.DefaultIfEmpty()
            where (termo == null
                       || EF.Functions.Like(conta.Descricao.ToLower(), termo)
                       || EF.Functions.Like(recebedor.Nome.ToLower(), termo)
                       || (responsavel != null && EF.Functions.Like(responsavel.Nome.ToLower(), termo)))
                  && (ativa == null || regra.Ativa == ativa)
                  && (dataInicial == null || regra.DataInicio >= dataInicial)
                  && (dataFinal == null || regra.DataInicio <= dataFinal)
            select new RecorrenciaRow(
                regra.Id, regra.TipoPeriodicidade, regra.TipoDia, regra.DiaOrdemMensal,
                regra.DataInicio, regra.DataFim, regra.Ativa, regra.PermiteEdicaoOcorrenciaIndividual, regra.Observacao,
                "ContaPagar", conta.Id, conta.Descricao, conta.ValorLiquido,
                recebedor.Nome, responsavel.Nome);

        return consulta.ToListAsync(cancellationToken);
    }

    private Task<List<RecorrenciaRow>> ConsultarOrigensReceberAsync(
        RecorrenciaListQueryRequest query, string? termo, CancellationToken cancellationToken)
    {
        var ativa = query.Ativa;
        var dataInicial = query.DataReferenciaInicial;
        var dataFinal = query.DataReferenciaFinal;

        var consulta =
            from conta in dbContext.ContasReceber.AsNoTracking()
            where conta.RegraRecorrenciaId.HasValue
                  && conta.Origem != DomainOrigemLancamento.Recorrencia
                  && !dbContext.ContasReceber.Any(outra =>
                      outra.RegraRecorrenciaId == conta.RegraRecorrenciaId
                      && outra.Origem != DomainOrigemLancamento.Recorrencia
                      && outra.CreatedAtUtc < conta.CreatedAtUtc)
            join regra in dbContext.RegrasRecorrencia.AsNoTracking() on conta.RegraRecorrenciaId!.Value equals regra.Id
            join pagador in dbContext.Pessoas.AsNoTracking() on conta.PagadorId equals pagador.Id
            join responsavel in dbContext.Pessoas.AsNoTracking() on conta.ResponsavelId equals responsavel.Id into responsaveis
            from responsavel in responsaveis.DefaultIfEmpty()
            where (termo == null
                       || EF.Functions.Like(conta.Descricao.ToLower(), termo)
                       || EF.Functions.Like(pagador.Nome.ToLower(), termo)
                       || (responsavel != null && EF.Functions.Like(responsavel.Nome.ToLower(), termo)))
                  && (ativa == null || regra.Ativa == ativa)
                  && (dataInicial == null || regra.DataInicio >= dataInicial)
                  && (dataFinal == null || regra.DataInicio <= dataFinal)
            select new RecorrenciaRow(
                regra.Id, regra.TipoPeriodicidade, regra.TipoDia, regra.DiaOrdemMensal,
                regra.DataInicio, regra.DataFim, regra.Ativa, regra.PermiteEdicaoOcorrenciaIndividual, regra.Observacao,
                "ContaReceber", conta.Id, conta.Descricao, conta.ValorLiquido,
                pagador.Nome, responsavel.Nome);

        return consulta.ToListAsync(cancellationToken);
    }

    private static IEnumerable<RecorrenciaRow> Ordenar<TKey>(
        IEnumerable<RecorrenciaRow> rows,
        RecorrenciaListQueryRequest query,
        Func<RecorrenciaRow, TKey> keySelector)
    {
        return query.SortDirection == SortDirection.Desc
            ? rows.OrderByDescending(keySelector).ThenByDescending(x => x.Descricao)
            : rows.OrderBy(keySelector).ThenBy(x => x.Descricao);
    }

    private static RecorrenciaListItemResponse MapearRow(RecorrenciaRow row)
    {
        return new RecorrenciaListItemResponse(
            row.Id,
            (TipoPeriodicidadeRecorrencia)row.TipoPeriodicidade,
            (TipoDiaRecorrencia)row.TipoDia,
            row.DiaOrdemMensal,
            row.DataInicio,
            row.DataFim,
            row.Ativa,
            row.PermiteEdicaoOcorrenciaIndividual,
            row.Observacao,
            row.ContaOrigemTipo,
            row.ContaOrigemId,
            row.Descricao,
            row.ValorLiquido,
            row.PessoaNome,
            row.ResponsavelNome);
    }

    private sealed record RecorrenciaRow(
        Guid Id,
        DomainTipoPeriodicidade TipoPeriodicidade,
        DomainTipoDia TipoDia,
        int DiaOrdemMensal,
        DateOnly DataInicio,
        DateOnly? DataFim,
        bool Ativa,
        bool PermiteEdicaoOcorrenciaIndividual,
        string? Observacao,
        string ContaOrigemTipo,
        Guid ContaOrigemId,
        string Descricao,
        decimal ValorLiquido,
        string PessoaNome,
        string? ResponsavelNome);
}
