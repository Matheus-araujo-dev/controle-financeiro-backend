using ControleFinanceiro.Application.Common.Pagination;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Contracts.Financeiro.Movimentacoes;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Financeiro.Movimentacoes;

public sealed class MovimentacaoFinanceiraAppService(IAppDbContext dbContext)
{
    public async Task<PagedResult<MovimentacaoResumoResponse>> ListarAsync(
        MovimentacaoListQueryRequest query,
        CancellationToken cancellationToken)
    {
        var consulta =
            from movimento in dbContext.MovimentacoesFinanceiras.AsNoTracking()
            join status in dbContext.StatusMovimentacoes.AsNoTracking() on movimento.StatusMovimentacaoId equals status.Id
            join contaBancaria in dbContext.ContasBancarias.AsNoTracking() on movimento.ContaBancariaId equals contaBancaria.Id into contasJoin
            from contaBancaria in contasJoin.DefaultIfEmpty()
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
                movimento.Observacao,
                movimento.DataConciliacao,
                movimento.CreatedAtUtc,
                movimento.UpdatedAtUtc
            };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var termo = query.Search.Trim().ToLower();
            consulta = consulta.Where(x => (x.Observacao ?? string.Empty).ToLower().Contains(termo));
        }

        if (query.ContaBancariaId.HasValue)
        {
            consulta = consulta.Where(x => x.ContaBancariaId == query.ContaBancariaId.Value);
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

        consulta = query.SortDirection == SortDirection.Desc
            ? consulta.OrderByDescending(x => x.DataMovimentacao).ThenByDescending(x => x.Id)
            : consulta.OrderBy(x => x.DataMovimentacao).ThenBy(x => x.Id);

        var totalItems = await consulta.CountAsync(cancellationToken);
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
                x.Observacao))
            .ToArray();

        return PagedResult<MovimentacaoResumoResponse>.Create(items, query.Page, query.PageSize, totalItems);
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
                movimento.Observacao,
                movimento.DataConciliacao,
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
                item.Observacao,
                item.DataConciliacao,
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

}
