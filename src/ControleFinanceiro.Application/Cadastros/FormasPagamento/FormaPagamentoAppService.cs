using ControleFinanceiro.Application.Common.Pagination;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Contracts.Cadastros.FormasPagamento;
using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Domain.Cadastros.FormasPagamento;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Cadastros.FormasPagamento;

public sealed class FormaPagamentoAppService(IAppDbContext dbContext)
{
    public async Task<PagedResult<FormaPagamentoResumoResponse>> ListarAsync(
        FormaPagamentoListQueryRequest query,
        CancellationToken cancellationToken)
    {
        var consulta = dbContext.FormasPagamento.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var termo = query.Search.Trim().ToLower();
            consulta = consulta.Where(x => x.Nome.ToLower().Contains(termo));
        }

        if (query.Tipo.HasValue)
        {
            consulta = consulta.Where(x => x.Tipo == MapearTipo(query.Tipo.Value));
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

        consulta = query.SortDirection == SortDirection.Desc
            ? consulta.OrderByDescending(x => x.Nome)
            : consulta.OrderBy(x => x.Nome);

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
        var formaPagamento = await dbContext.FormasPagamento.AsNoTracking()
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

        dbContext.FormasPagamento.Add(formaPagamento);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await ObterPorIdAsync(formaPagamento.Id, cancellationToken)
            ?? throw new InvalidOperationException("Forma de pagamento criada nao foi encontrada.");
    }

    public async Task<FormaPagamentoDetalheResponse?> AtualizarAsync(
        Guid id,
        AtualizarFormaPagamentoRequest request,
        CancellationToken cancellationToken)
    {
        var formaPagamento = await dbContext.FormasPagamento.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

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

        await dbContext.SaveChangesAsync(cancellationToken);

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
