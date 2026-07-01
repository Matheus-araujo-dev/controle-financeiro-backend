using ControleFinanceiro.Application.Common.Cache;
using ControleFinanceiro.Application.Common.Extensions;
using ControleFinanceiro.Application.Common.Pagination;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Contracts.Financeiro.Movimentacoes;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Financeiro.Movimentacoes;

public sealed class MovimentacaoFinanceiraAppService(IAppDbContext dbContext, ILookupCacheService lookupCache)
{
    private readonly ILookupCacheService _lookupCache = lookupCache;

    public async Task<MovimentacaoListResponse> ListarAsync(
        MovimentacaoListQueryRequest query,
        CancellationToken cancellationToken)
    {
        var consulta =
            from movimento in dbContext.MovimentacoesFinanceiras.AsNoTracking()
            join status in dbContext.StatusMovimentacoes.AsNoTracking() on movimento.StatusMovimentacaoId equals status.Id
            join contaBancaria in dbContext.ContasBancarias.AsNoTracking() on movimento.ContaBancariaId equals contaBancaria.Id into contasJoin
            from contaBancaria in contasJoin.DefaultIfEmpty()
            join contaPagar in dbContext.ContasPagar.AsNoTracking() on movimento.ContaPagarId equals contaPagar.Id into contasPagarJoin
            from contaPagar in contasPagarJoin.DefaultIfEmpty()
            join contaReceber in dbContext.ContasReceber.AsNoTracking() on movimento.ContaReceberId equals contaReceber.Id into contasReceberJoin
            from contaReceber in contasReceberJoin.DefaultIfEmpty()
            join responsavelPagar in dbContext.Pessoas.AsNoTracking() on contaPagar.ResponsavelCompraId equals responsavelPagar.Id into respPagarJoin
            from responsavelPagar in respPagarJoin.DefaultIfEmpty()
            join responsavelReceber in dbContext.Pessoas.AsNoTracking() on contaReceber.ResponsavelId equals responsavelReceber.Id into respReceberJoin
            from responsavelReceber in respReceberJoin.DefaultIfEmpty()
            select new
            {
                movimento.Id,
                movimento.DataMovimentacao,
                movimento.Tipo,
                movimento.Natureza,
                StatusCodigo = status.Codigo,
                StatusNome = status.Nome,
                movimento.Valor,
                movimento.ContaBancariaId,
                ContaBancariaNome = contaBancaria != null ? contaBancaria.Nome : null,
                movimento.ContaPagarId,
                movimento.ContaReceberId,
                movimento.FaturaCartaoId,
                ContaPagarResponsavelId = contaPagar != null ? contaPagar.ResponsavelCompraId : null,
                ContaReceberResponsavelId = contaReceber != null ? contaReceber.ResponsavelId : null,
                ResponsavelNome = responsavelPagar != null ? responsavelPagar.Nome : (responsavelReceber != null ? responsavelReceber.Nome : null),
                movimento.Observacao,
                movimento.CreatedAtUtc,
                movimento.UpdatedAtUtc
            };

        consulta = consulta.Where(x => x.StatusCodigo != "CANCELADA");

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var termo = $"%{query.Search.Trim().ToLower()}%";
            consulta = consulta.Where(x => EF.Functions.Like((x.Observacao ?? "").ToLower(), termo));
        }

        var contaBancariaIds = NormalizarGuidList(query.ContaBancariaIds);

        if (contaBancariaIds.Length > 0)
        {
            consulta = consulta.Where(x => x.ContaBancariaId.HasValue)
                .WhereIn(x => x.ContaBancariaId!.Value, contaBancariaIds);
        }
        else if (query.ContaBancariaId.HasValue)
        {
            consulta = consulta.Where(x => x.ContaBancariaId == query.ContaBancariaId.Value);
        }

        var responsavelIds = NormalizarGuidList(query.ResponsavelIds);

        if (responsavelIds.Length > 0)
        {
            consulta = consulta.Where(x =>
                (x.ContaPagarResponsavelId.HasValue && responsavelIds.Contains(x.ContaPagarResponsavelId.Value)) ||
                (x.ContaReceberResponsavelId.HasValue && responsavelIds.Contains(x.ContaReceberResponsavelId.Value)));
        }

        if (!string.IsNullOrWhiteSpace(query.StatusCodigo))
        {
            var statusCodigo = query.StatusCodigo.Trim().ToUpperInvariant();
            consulta = consulta.Where(x => x.StatusCodigo == statusCodigo);
        }

        if (query.Tipo.HasValue)
        {
            var tipo = MapearTipo(query.Tipo.Value);
            consulta = consulta.Where(x => x.Tipo == tipo);
        }

        if (query.Natureza.HasValue)
        {
            var natureza = MapearNatureza(query.Natureza.Value);
            consulta = consulta.Where(x => x.Natureza == natureza);
        }

        if (query.DataInicial.HasValue)
        {
            consulta = consulta.Where(x => x.DataMovimentacao >= query.DataInicial.Value);
        }

        if (query.DataFinal.HasValue)
        {
            consulta = consulta.Where(x => x.DataMovimentacao <= query.DataFinal.Value);
        }

        consulta = (query.SortBy ?? string.Empty).ToLowerInvariant() switch
        {
            "observacao" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.Observacao).ThenByDescending(x => x.DataMovimentacao)
                : consulta.OrderBy(x => x.Observacao).ThenBy(x => x.DataMovimentacao),
            "natureza" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.Natureza).ThenByDescending(x => x.DataMovimentacao)
                : consulta.OrderBy(x => x.Natureza).ThenBy(x => x.DataMovimentacao),
            "contabancarianome" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.ContaBancariaNome).ThenByDescending(x => x.DataMovimentacao)
                : consulta.OrderBy(x => x.ContaBancariaNome).ThenBy(x => x.DataMovimentacao),
            "responsavelnome" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.ResponsavelNome).ThenByDescending(x => x.DataMovimentacao)
                : consulta.OrderBy(x => x.ResponsavelNome).ThenBy(x => x.DataMovimentacao),
            "valor" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.Valor).ThenByDescending(x => x.DataMovimentacao)
                : consulta.OrderBy(x => x.Valor).ThenBy(x => x.DataMovimentacao),
            _ => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.DataMovimentacao).ThenByDescending(x => x.Id)
                : consulta.OrderBy(x => x.DataMovimentacao).ThenBy(x => x.Id)
        };

        var totalItems = await consulta.CountAsync(cancellationToken);
        var summaryProjection = await consulta
            .GroupBy(_ => 1)
            .Select(group => new
            {
                TotalEntradas = group
                    .Where(x => x.Tipo == TipoMovimentacao.Entrada)
                    .Sum(x => x.Valor),
                TotalSaidas = group
                    .Where(x => x.Tipo == TipoMovimentacao.Saida)
                    .Sum(x => x.Valor)
            })
            .SingleOrDefaultAsync(cancellationToken);
        var items = (await consulta
                .ApplyPagination(query)
                .ToArrayAsync(cancellationToken))
            .Select(x => new MovimentacaoResumoResponse(
                x.Id,
                x.DataMovimentacao,
                MapearTipo(x.Tipo),
                MapearNatureza(x.Natureza),
                x.StatusCodigo,
                x.StatusNome,
                x.Valor,
                x.ContaBancariaId,
                x.ContaBancariaNome,
                x.ContaPagarId,
                x.ContaReceberId,
                x.FaturaCartaoId,
                x.Observacao,
                x.ResponsavelNome))
            .ToArray();

        var paged = PagedResult<MovimentacaoResumoResponse>.Create(items, query.Page, query.PageSize, totalItems);
        var totalEntradas = decimal.Round(summaryProjection?.TotalEntradas ?? 0m, 2, MidpointRounding.AwayFromZero);
        var totalSaidas = decimal.Round(summaryProjection?.TotalSaidas ?? 0m, 2, MidpointRounding.AwayFromZero);

        return new MovimentacaoListResponse(
            paged.Items,
            paged.Page,
            paged.PageSize,
            paged.TotalItems,
            paged.TotalPages,
            new MovimentacaoListSummaryResponse(
                totalItems,
                totalEntradas,
                totalSaidas,
                decimal.Round(totalEntradas - totalSaidas, 2, MidpointRounding.AwayFromZero)));
    }

    public async Task<MovimentacaoDetalheResponse?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await (
            from movimento in dbContext.MovimentacoesFinanceiras.AsNoTracking()
            join status in dbContext.StatusMovimentacoes.AsNoTracking() on movimento.StatusMovimentacaoId equals status.Id
            join contaBancaria in dbContext.ContasBancarias.AsNoTracking() on movimento.ContaBancariaId equals contaBancaria.Id into contasJoin
            from contaBancaria in contasJoin.DefaultIfEmpty()
            where movimento.Id == id
            select new
            {
                movimento.Id,
                movimento.DataMovimentacao,
                movimento.Tipo,
                movimento.Natureza,
                StatusCodigo = status.Codigo,
                StatusNome = status.Nome,
                movimento.Valor,
                movimento.ContaBancariaId,
                ContaBancariaNome = contaBancaria != null ? contaBancaria.Nome : null,
                movimento.ContaPagarId,
                movimento.ContaReceberId,
                movimento.FaturaCartaoId,
                movimento.Observacao,
                movimento.CreatedAtUtc,
                movimento.UpdatedAtUtc
            })
            .SingleOrDefaultAsync(cancellationToken);

        return item is null
            ? null
            : new MovimentacaoDetalheResponse(
                item.Id,
                item.DataMovimentacao,
                MapearTipo(item.Tipo),
                MapearNatureza(item.Natureza),
                item.StatusCodigo,
                item.StatusNome,
                item.Valor,
                item.ContaBancariaId,
                item.ContaBancariaNome,
                item.ContaPagarId,
                item.ContaReceberId,
                item.FaturaCartaoId,
                item.Observacao,
                item.CreatedAtUtc,
                item.UpdatedAtUtc);
    }

    private static TipoMovimentacao MapearTipo(TipoMovimentacaoResponse tipo)
    {
        return tipo switch
        {
            TipoMovimentacaoResponse.Entrada => TipoMovimentacao.Entrada,
            TipoMovimentacaoResponse.Saida => TipoMovimentacao.Saida,
            _ => throw new ArgumentOutOfRangeException(nameof(tipo))
        };
    }

    private static TipoMovimentacaoResponse MapearTipo(TipoMovimentacao tipo)
    {
        return tipo switch
        {
            TipoMovimentacao.Entrada => TipoMovimentacaoResponse.Entrada,
            TipoMovimentacao.Saida => TipoMovimentacaoResponse.Saida,
            _ => throw new ArgumentOutOfRangeException(nameof(tipo))
        };
    }

    private static NaturezaMovimentacao MapearNatureza(NaturezaMovimentacaoResponse natureza)
    {
        return natureza switch
        {
            NaturezaMovimentacaoResponse.Prevista => NaturezaMovimentacao.Prevista,
            NaturezaMovimentacaoResponse.Realizada => NaturezaMovimentacao.Realizada,
            NaturezaMovimentacaoResponse.Economica => NaturezaMovimentacao.Economica,
            _ => throw new ArgumentOutOfRangeException(nameof(natureza))
        };
    }

    private static NaturezaMovimentacaoResponse MapearNatureza(NaturezaMovimentacao natureza)
    {
        return natureza switch
        {
            NaturezaMovimentacao.Prevista => NaturezaMovimentacaoResponse.Prevista,
            NaturezaMovimentacao.Realizada => NaturezaMovimentacaoResponse.Realizada,
            NaturezaMovimentacao.Economica => NaturezaMovimentacaoResponse.Economica,
            _ => throw new ArgumentOutOfRangeException(nameof(natureza))
        };
    }

    private static Guid[] NormalizarGuidList(string? values)
    {
        if (string.IsNullOrWhiteSpace(values))
        {
            return [];
        }

        return values
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(value => Guid.TryParse(value, out var parsed) ? parsed : Guid.Empty)
            .Where(value => value != Guid.Empty)
            .Distinct()
            .ToArray();
    }

}
