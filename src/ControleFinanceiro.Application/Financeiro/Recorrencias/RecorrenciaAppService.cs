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
    IContaPagarRecorrenciaService contaPagarRecorrenciaService,
    IContaReceberRecorrenciaService contaReceberRecorrenciaService,
    ILogger<RecorrenciaAppService> logger)
{
    private static DateOnly HorizonteSeisMeses(DateOnly referencia)
    {
        var horizonte = referencia.AddMonths(6);
        return new DateOnly(horizonte.Year, horizonte.Month, DateTime.DaysInMonth(horizonte.Year, horizonte.Month));
    }

    public async Task<GerarOcorrenciasResultResponse> GerarOcorrenciasRecorrentesNoMesAsync(DateOnly dataReferencia, CancellationToken cancellationToken)
    {
        var ateData = HorizonteSeisMeses(dataReferencia);
        logger.LogInformation(
            "Iniciando geração automática de recorrências — referência {Data}, horizonte até {AteData}.",
            dataReferencia, ateData);

        var regrasAtivas = await dbContext.RegrasRecorrencia
            .Where(x => x.Ativa)
            .ToArrayAsync(cancellationToken);

        logger.LogInformation("Regras ativas encontradas: {Total}.", regrasAtivas.Length);

        int totalProcessadas = 0;
        int totalGeradas = 0;
        int totalErros = 0;

        foreach (var regra in regrasAtivas)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (regra.FamiliaId != Guid.Empty)
                {
                    dbContext.DefinirFamiliaCorrente(regra.FamiliaId);
                }

                int geradas;
                if (regra.TipoLancamento == Domain.Financeiro.TipoLancamentoRecorrencia.ContaPagar)
                {
                    geradas = await contaPagarRecorrenciaService.GerarPorRegraAsync(regra, ateData, cancellationToken);
                }
                else
                {
                    geradas = await contaReceberRecorrenciaService.GerarPorRegraAsync(regra, ateData, cancellationToken);
                }

                if (geradas > 0)
                    logger.LogInformation("Regra {RegraId}: {Geradas} ocorrência(s) gerada(s).", regra.Id, geradas);

                totalGeradas += geradas;
                totalProcessadas++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                totalErros++;
                logger.LogError(ex, "Erro ao gerar ocorrências para a regra {RegraId} (família {FamiliaId}).", regra.Id, regra.FamiliaId);
                // Entidades com falha ficam no change tracker; limpar para não contaminar iterações seguintes.
                dbContext.LimparChangeTracker();
            }
        }

        logger.LogInformation(
            "Geração automática concluída. Processadas: {Processadas}, Geradas: {Geradas}, Erros: {Erros}.",
            totalProcessadas, totalGeradas, totalErros);

        return new GerarOcorrenciasResultResponse(regrasAtivas.Length, totalProcessadas, totalGeradas, totalErros);
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

    public async Task<RecorrenciaListItemResponse?> ObterAsync(Guid id, CancellationToken cancellationToken)
    {
        var pagar = await (
            from regra in dbContext.RegrasRecorrencia.AsNoTracking()
            where regra.Id == id
            join conta in dbContext.ContasPagar.AsNoTracking()
                on regra.Id equals conta.RegraRecorrenciaId
            where conta.Origem != DomainOrigemLancamento.Recorrencia
            join recebedor in dbContext.Pessoas.AsNoTracking() on conta.RecebedorId equals recebedor.Id
            join responsavel in dbContext.Pessoas.AsNoTracking() on conta.ResponsavelCompraId equals responsavel.Id into responsaveis
            from responsavel in responsaveis.DefaultIfEmpty()
            orderby conta.CreatedAtUtc
            select new RecorrenciaRow(
                regra.Id, regra.TipoPeriodicidade, regra.TipoDia, regra.DiaOrdemMensal,
                regra.DataInicio, regra.DataFim, regra.Ativa, regra.PermiteEdicaoOcorrenciaIndividual, regra.Observacao,
                "ContaPagar", conta.Id, conta.Descricao, conta.ValorLiquido,
                recebedor.Nome, responsavel.Nome)
        ).FirstOrDefaultAsync(cancellationToken);

        if (pagar != null) return MapearRow(pagar);

        var receber = await (
            from regra in dbContext.RegrasRecorrencia.AsNoTracking()
            where regra.Id == id
            join conta in dbContext.ContasReceber.AsNoTracking()
                on regra.Id equals conta.RegraRecorrenciaId
            where conta.Origem != DomainOrigemLancamento.Recorrencia
            join pagador in dbContext.Pessoas.AsNoTracking() on conta.PagadorId equals pagador.Id
            join responsavel in dbContext.Pessoas.AsNoTracking() on conta.ResponsavelId equals responsavel.Id into responsaveis
            from responsavel in responsaveis.DefaultIfEmpty()
            orderby conta.CreatedAtUtc
            select new RecorrenciaRow(
                regra.Id, regra.TipoPeriodicidade, regra.TipoDia, regra.DiaOrdemMensal,
                regra.DataInicio, regra.DataFim, regra.Ativa, regra.PermiteEdicaoOcorrenciaIndividual, regra.Observacao,
                "ContaReceber", conta.Id, conta.Descricao, conta.ValorLiquido,
                pagador.Nome, responsavel.Nome)
        ).FirstOrDefaultAsync(cancellationToken);

        return receber != null ? MapearRow(receber) : null;
    }

    public async Task<RecorrenciaListItemResponse> PausarAsync(Guid id, CancellationToken cancellationToken)
    {
        var regra = await dbContext.RegrasRecorrencia
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Recorrência não encontrada.");

        regra.Pausar();

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        await contaPagarRecorrenciaService.CancelarFuturasNaoPagasAsync(regra.Id, hoje, cancellationToken);
        await contaReceberRecorrenciaService.CancelarFuturasNaoPagasAsync(regra.Id, hoje, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return (await ObterAsync(id, cancellationToken))!;
    }

    public async Task<RecorrenciaListItemResponse> RetomarAsync(Guid id, CancellationToken cancellationToken)
    {
        var regra = await dbContext.RegrasRecorrencia
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Recorrência não encontrada.");

        regra.Retomar();
        await dbContext.SaveChangesAsync(cancellationToken);

        var ateData = HorizonteSeisMeses(DateOnly.FromDateTime(DateTime.UtcNow));
        if (regra.TipoLancamento == Domain.Financeiro.TipoLancamentoRecorrencia.ContaPagar)
        {
            await contaPagarRecorrenciaService.GerarPorRegraAsync(regra, ateData, cancellationToken);
        }
        else
        {
            await contaReceberRecorrenciaService.GerarPorRegraAsync(regra, ateData, cancellationToken);
        }

        return (await ObterAsync(id, cancellationToken))!;
    }

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
