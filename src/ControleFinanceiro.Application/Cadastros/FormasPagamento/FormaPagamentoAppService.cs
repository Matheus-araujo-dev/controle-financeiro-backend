using ControleFinanceiro.Application.Common.Cache;
using ControleFinanceiro.Application.Common.Pagination;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Contracts.Cadastros.FormasPagamento;
using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Domain.Cadastros.FormasPagamento;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Cadastros.FormasPagamento;

public sealed class FormaPagamentoAppService
{
    private readonly IAppDbContext _dbContext;
    private readonly ILookupCacheService _lookupCache;

    public FormaPagamentoAppService(IAppDbContext dbContext, ILookupCacheService lookupCache)
    {
        _dbContext = dbContext;
        _lookupCache = lookupCache;
    }

    public async Task<IReadOnlyCollection<FormaPagamentoResumoResponse>> ListarCacheAsync(CancellationToken cancellationToken)
    {
        var items = await _lookupCache.GetAllFormaPagamentoAsync(cancellationToken);
        return items
            .Select(x => new FormaPagamentoResumoResponse(
                x.Id,
                x.Nome,
                MapearTipo(x.Tipo),
                x.EhCartao,
                x.BaixarAutomaticamente,
                x.Ativo))
            .ToList();
    }

    public async Task<PagedResult<FormaPagamentoResumoResponse>> ListarAsync(
        FormaPagamentoListQueryRequest query,
        CancellationToken cancellationToken)
    {
        var consulta = _dbContext.FormasPagamento.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var termo = $"%{query.Search.Trim().ToLower()}%";
            consulta = consulta.Where(x => EF.Functions.Like(x.Nome.ToLower(), termo));
        }

        if (query.Tipo.HasValue)
        {
            consulta = consulta.Where(x => x.Tipo == MapearTipo(query.Tipo.Value));
        }

        if (query.Tipos is { Count: > 0 })
        {
            var tipos = query.Tipos.Select(MapearTipo).ToArray();
            consulta = consulta.Where(x => tipos.Contains(x.Tipo));
        }

        if (query.EhCartao.HasValue)
        {
            consulta = consulta.Where(x => x.EhCartao == query.EhCartao.Value);
        }

        if (query.BaixarAutomaticamente.HasValue)
        {
            consulta = consulta.Where(x => x.BaixarAutomaticamente == query.BaixarAutomaticamente.Value);
        }

        if (query.Ativo.HasValue)
        {
            consulta = consulta.Where(x => x.Ativo == query.Ativo.Value);
        }

        consulta = (query.SortBy ?? string.Empty).ToLowerInvariant() switch
        {
            "tipo" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.Tipo).ThenByDescending(x => x.Nome)
                : consulta.OrderBy(x => x.Tipo).ThenBy(x => x.Nome),
            "ehcartao" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.EhCartao).ThenByDescending(x => x.Nome)
                : consulta.OrderBy(x => x.EhCartao).ThenBy(x => x.Nome),
            "baixarautomaticamente" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.BaixarAutomaticamente).ThenByDescending(x => x.Nome)
                : consulta.OrderBy(x => x.BaixarAutomaticamente).ThenBy(x => x.Nome),
            "ativo" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.Ativo).ThenByDescending(x => x.Nome)
                : consulta.OrderBy(x => x.Ativo).ThenBy(x => x.Nome),
            _ => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.Nome)
                : consulta.OrderBy(x => x.Nome)
        };

        var totalItems = await consulta.CountAsync(cancellationToken);
        var entidades = await consulta.ApplyPagination(query).ToListAsync(cancellationToken);
        var items = entidades
            .Select(x => new FormaPagamentoResumoResponse(
                x.Id,
                x.Nome,
                MapearTipo(x.Tipo),
                x.EhCartao,
                x.BaixarAutomaticamente,
                x.Ativo))
            .ToArray();

        return PagedResult<FormaPagamentoResumoResponse>.Create(items, query.Page, query.PageSize, totalItems);
    }

    public async Task<FormaPagamentoDetalheResponse?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var formaPagamento = await _dbContext.FormasPagamento.AsNoTracking()
            .Where(x => x.Id == id)
            .SingleOrDefaultAsync(cancellationToken);

        return formaPagamento is null
            ? null
            : new FormaPagamentoDetalheResponse(
                formaPagamento.Id,
                formaPagamento.Nome,
                MapearTipo(formaPagamento.Tipo),
                formaPagamento.EhCartao,
                formaPagamento.BaixarAutomaticamente,
                formaPagamento.Ativo,
                formaPagamento.CreatedAtUtc,
                formaPagamento.UpdatedAtUtc);
    }

    public async Task<FormaPagamentoDetalheResponse> CriarAsync(
        CriarFormaPagamentoRequest request,
        CancellationToken cancellationToken)
    {
        FormaPagamento formaPagamento;

        try
        {
            formaPagamento = FormaPagamento.Criar(
                request.Nome,
                MapearTipo(request.Tipo),
                request.EhCartao,
                request.BaixarAutomaticamente,
                request.Ativo);
        }
        catch (ArgumentException exception)
        {
            throw ValidationExceptionFactory.Create("Nome", exception.Message);
        }

        _dbContext.FormasPagamento.Add(formaPagamento);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _lookupCache.RefreshFormaPagamentoAsync(cancellationToken);

        return await ObterPorIdAsync(formaPagamento.Id, cancellationToken)
            ?? throw new InvalidOperationException("Forma de pagamento criada não foi encontrada.");
    }

    public async Task<FormaPagamentoDetalheResponse?> AtualizarAsync(
        Guid id,
        AtualizarFormaPagamentoRequest request,
        CancellationToken cancellationToken)
    {
        var formaPagamento = await _dbContext.FormasPagamento.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (formaPagamento is null)
        {
            return null;
        }

        try
        {
            formaPagamento.Atualizar(
                request.Nome,
                MapearTipo(request.Tipo),
                request.EhCartao,
                request.BaixarAutomaticamente,
                request.Ativo);
        }
        catch (ArgumentException exception)
        {
            throw ValidationExceptionFactory.Create("Nome", exception.Message);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _lookupCache.RefreshFormaPagamentoAsync(cancellationToken);

        return await ObterPorIdAsync(id, cancellationToken);
    }

    private static TipoFormaPagamento MapearTipo(FormaPagamentoTipo tipo)
    {
        return tipo switch
        {
            FormaPagamentoTipo.Dinheiro => TipoFormaPagamento.Dinheiro,
            FormaPagamentoTipo.Pix => TipoFormaPagamento.Pix,
            FormaPagamentoTipo.Boleto => TipoFormaPagamento.Boleto,
            FormaPagamentoTipo.Transferencia => TipoFormaPagamento.Transferencia,
            FormaPagamentoTipo.Debito => TipoFormaPagamento.Debito,
            FormaPagamentoTipo.Credito => TipoFormaPagamento.Credito,
            FormaPagamentoTipo.Outro => TipoFormaPagamento.Outro,
            _ => throw new ArgumentOutOfRangeException(nameof(tipo))
        };
    }

    private static FormaPagamentoTipo MapearTipo(TipoFormaPagamento tipo)
    {
        return tipo switch
        {
            TipoFormaPagamento.Dinheiro => FormaPagamentoTipo.Dinheiro,
            TipoFormaPagamento.Pix => FormaPagamentoTipo.Pix,
            TipoFormaPagamento.Boleto => FormaPagamentoTipo.Boleto,
            TipoFormaPagamento.Transferencia => FormaPagamentoTipo.Transferencia,
            TipoFormaPagamento.Debito => FormaPagamentoTipo.Debito,
            TipoFormaPagamento.Credito => FormaPagamentoTipo.Credito,
            TipoFormaPagamento.Outro => FormaPagamentoTipo.Outro,
            _ => throw new ArgumentOutOfRangeException(nameof(tipo))
        };
    }
}
