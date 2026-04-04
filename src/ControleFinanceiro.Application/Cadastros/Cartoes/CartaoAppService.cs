using ControleFinanceiro.Application.Common.Pagination;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Contracts.Cadastros.Cartoes;
using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Domain.Cadastros.Cartoes;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Cadastros.Cartoes;

public sealed class CartaoAppService(IAppDbContext dbContext)
{
    public async Task<PagedResult<CartaoResumoResponse>> ListarAsync(
        CartaoListQueryRequest query,
        CancellationToken cancellationToken)
    {
        var consulta = dbContext.Cartoes.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var termo = query.Search.Trim().ToLower();
            consulta = consulta.Where(x =>
                x.Nome.ToLower().Contains(termo) ||
                x.Bandeira.ToLower().Contains(termo) ||
                x.NumeroFinal.ToLower().Contains(termo));
        }

        if (!string.IsNullOrWhiteSpace(query.Bandeira))
        {
            var bandeira = query.Bandeira.Trim().ToLower();
            consulta = consulta.Where(x => x.Bandeira.ToLower().Contains(bandeira));
        }

        if (query.Ativo.HasValue)
        {
            consulta = consulta.Where(x => x.Ativo == query.Ativo.Value);
        }

        consulta = query.SortDirection == SortDirection.Desc
            ? consulta.OrderByDescending(x => x.Nome)
            : consulta.OrderBy(x => x.Nome);

        var totalItems = await consulta.CountAsync(cancellationToken);
        var items = await consulta.ApplyPagination(query)
            .Select(x => new CartaoResumoResponse(
                x.Id,
                x.Nome,
                x.Bandeira,
                x.NumeroFinal,
                x.DiaFechamentoFatura,
                x.DiaVencimentoFatura,
                x.ContaBancariaPagamentoPadraoId,
                x.LimiteCredito,
                x.Ativo))
            .ToListAsync(cancellationToken);

        return PagedResult<CartaoResumoResponse>.Create(items, query.Page, query.PageSize, totalItems);
    }

    public async Task<CartaoDetalheResponse?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await dbContext.Cartoes.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new CartaoDetalheResponse(
                x.Id,
                x.Nome,
                x.Bandeira,
                x.NumeroFinal,
                x.DiaFechamentoFatura,
                x.DiaVencimentoFatura,
                x.ContaBancariaPagamentoPadraoId,
                x.LimiteCredito,
                x.Ativo,
                x.CreatedAtUtc,
                x.UpdatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<CartaoDetalheResponse> CriarAsync(CriarCartaoRequest request, CancellationToken cancellationToken)
    {
        await ValidarContaBancariaPadraoAsync(request.ContaBancariaPagamentoPadraoId, cancellationToken);

        Cartao cartao;

        try
        {
            cartao = Cartao.Criar(
                request.Nome,
                request.Bandeira,
                request.NumeroFinal,
                request.DiaFechamentoFatura,
                request.DiaVencimentoFatura,
                request.ContaBancariaPagamentoPadraoId,
                request.LimiteCredito,
                request.Ativo);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            throw ConverterParaValidacao(exception);
        }

        dbContext.Cartoes.Add(cartao);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await ObterPorIdAsync(cartao.Id, cancellationToken)
            ?? throw new InvalidOperationException("Cartao criado nao foi encontrado.");
    }

    public async Task<CartaoDetalheResponse?> AtualizarAsync(
        Guid id,
        AtualizarCartaoRequest request,
        CancellationToken cancellationToken)
    {
        var cartao = await dbContext.Cartoes.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (cartao is null)
        {
            return null;
        }

        await ValidarContaBancariaPadraoAsync(request.ContaBancariaPagamentoPadraoId, cancellationToken);

        try
        {
            cartao.Atualizar(
                request.Nome,
                request.Bandeira,
                request.NumeroFinal,
                request.DiaFechamentoFatura,
                request.DiaVencimentoFatura,
                request.ContaBancariaPagamentoPadraoId,
                request.LimiteCredito,
                request.Ativo);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            throw ConverterParaValidacao(exception);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return await ObterPorIdAsync(id, cancellationToken);
    }

    private async Task ValidarContaBancariaPadraoAsync(Guid? contaBancariaId, CancellationToken cancellationToken)
    {
        if (!contaBancariaId.HasValue)
        {
            return;
        }

        var existe = await dbContext.ContasBancarias.AnyAsync(x => x.Id == contaBancariaId.Value, cancellationToken);

        if (!existe)
        {
            throw ValidationExceptionFactory.Create("ContaBancariaPagamentoPadraoId", "Conta bancaria nao encontrada.");
        }
    }

    private static Exception ConverterParaValidacao(Exception exception)
    {
        var campo = exception switch
        {
            ArgumentException { ParamName: "nome" } => "Nome",
            ArgumentException { ParamName: "bandeira" } => "Bandeira",
            ArgumentException { ParamName: "numeroFinal" } => "NumeroFinal",
            ArgumentOutOfRangeException { ParamName: "diaFechamentoFatura" } => "DiaFechamentoFatura",
            ArgumentOutOfRangeException { ParamName: "diaVencimentoFatura" } => "DiaVencimentoFatura",
            _ => "Request"
        };

        return ValidationExceptionFactory.Create(campo, exception.Message);
    }
}
