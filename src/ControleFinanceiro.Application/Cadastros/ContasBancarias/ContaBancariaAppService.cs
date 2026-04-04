using ControleFinanceiro.Application.Common.Pagination;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Contracts.Cadastros.ContasBancarias;
using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Domain.Cadastros.ContasBancarias;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Cadastros.ContasBancarias;

public sealed class ContaBancariaAppService(IAppDbContext dbContext)
{
    public async Task<PagedResult<ContaBancariaResumoResponse>> ListarAsync(
        ContaBancariaListQueryRequest query,
        CancellationToken cancellationToken)
    {
        var consulta = dbContext.ContasBancarias.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var termo = query.Search.Trim().ToLower();
            consulta = consulta.Where(x =>
                x.Nome.ToLower().Contains(termo) ||
                x.Banco.ToLower().Contains(termo) ||
                (x.NumeroConta != null && x.NumeroConta.ToLower().Contains(termo)));
        }

        if (!string.IsNullOrWhiteSpace(query.Banco))
        {
            var banco = query.Banco.Trim().ToLower();
            consulta = consulta.Where(x => x.Banco.ToLower().Contains(banco));
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
            .Select(x => new ContaBancariaResumoResponse(
                x.Id,
                x.Nome,
                x.Banco,
                x.Agencia,
                x.NumeroConta,
                x.TipoConta,
                x.SaldoInicial,
                x.DataSaldoInicial,
                x.Ativo))
            .ToListAsync(cancellationToken);

        return PagedResult<ContaBancariaResumoResponse>.Create(items, query.Page, query.PageSize, totalItems);
    }

    public async Task<ContaBancariaDetalheResponse?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await dbContext.ContasBancarias.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new ContaBancariaDetalheResponse(
                x.Id,
                x.Nome,
                x.Banco,
                x.Agencia,
                x.NumeroConta,
                x.TipoConta,
                x.SaldoInicial,
                x.DataSaldoInicial,
                x.Ativo,
                x.CreatedAtUtc,
                x.UpdatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<ContaBancariaDetalheResponse> CriarAsync(
        CriarContaBancariaRequest request,
        CancellationToken cancellationToken)
    {
        ContaBancaria conta;

        try
        {
            conta = ContaBancaria.Criar(
                request.Nome,
                request.Banco,
                request.Agencia,
                request.NumeroConta,
                request.TipoConta,
                request.SaldoInicial,
                request.DataSaldoInicial,
                request.Ativo);
        }
        catch (ArgumentException exception)
        {
            throw ValidationExceptionFactory.Create(
                exception.ParamName == "banco" ? "Banco" : "Nome",
                exception.Message);
        }

        dbContext.ContasBancarias.Add(conta);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await ObterPorIdAsync(conta.Id, cancellationToken)
            ?? throw new InvalidOperationException("Conta bancaria criada nao foi encontrada.");
    }

    public async Task<ContaBancariaDetalheResponse?> AtualizarAsync(
        Guid id,
        AtualizarContaBancariaRequest request,
        CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasBancarias.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (conta is null)
        {
            return null;
        }

        try
        {
            conta.Atualizar(
                request.Nome,
                request.Banco,
                request.Agencia,
                request.NumeroConta,
                request.TipoConta,
                request.SaldoInicial,
                request.DataSaldoInicial,
                request.Ativo);
        }
        catch (ArgumentException exception)
        {
            throw ValidationExceptionFactory.Create(
                exception.ParamName == "banco" ? "Banco" : "Nome",
                exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return await ObterPorIdAsync(id, cancellationToken);
    }
}
