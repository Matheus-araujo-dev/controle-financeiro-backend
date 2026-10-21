using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Contracts.Cadastros.Pessoas;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Cadastros.Pessoas;

public sealed class PessoaResumoFinanceiroService(IAppDbContext dbContext)
{
    public async Task<PessoaResumoFinanceiroResponse?> ObterResumoAsync(Guid pessoaId, CancellationToken cancellationToken)
    {
        var pessoa = await dbContext.Pessoas.AsNoTracking()
            .Where(p => p.Id == pessoaId)
            .Select(p => new { p.Id, p.Nome })
            .SingleOrDefaultAsync(cancellationToken);

        if (pessoa is null) return null;

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);

        var contasPagar = await dbContext.ContasPagar.AsNoTracking()
            .Where(c => c.RecebedorId == pessoaId && c.StatusContaId != StatusConta.CanceladaId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalPendente = g.Where(c => c.StatusContaId != StatusConta.LiquidadaId && c.StatusContaId != StatusConta.EmFaturaId)
                    .Sum(c => (decimal?)c.ValorLiquido) ?? 0m,
                TotalLiquidado = g.Where(c => c.StatusContaId == StatusConta.LiquidadaId)
                    .Sum(c => (decimal?)c.ValorLiquido) ?? 0m,
                TotalVencido = g.Where(c => c.StatusContaId != StatusConta.LiquidadaId && c.StatusContaId != StatusConta.EmFaturaId && c.DataVencimento < hoje)
                    .Sum(c => (decimal?)c.ValorLiquido) ?? 0m,
                QuantidadeTotal = g.Count()
            })
            .SingleOrDefaultAsync(cancellationToken);

        var contasReceber = await dbContext.ContasReceber.AsNoTracking()
            .Where(c => c.PagadorId == pessoaId && c.StatusContaId != StatusConta.CanceladaId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalPendente = g.Where(c => c.StatusContaId != StatusConta.LiquidadaId)
                    .Sum(c => (decimal?)c.ValorLiquido) ?? 0m,
                TotalRecebido = g.Where(c => c.StatusContaId == StatusConta.LiquidadaId)
                    .Sum(c => (decimal?)c.ValorLiquido) ?? 0m,
                TotalVencido = g.Where(c => c.StatusContaId != StatusConta.LiquidadaId && c.DataVencimento < hoje)
                    .Sum(c => (decimal?)c.ValorLiquido) ?? 0m,
                QuantidadeTotal = g.Count()
            })
            .SingleOrDefaultAsync(cancellationToken);

        var reembolsoPendente = await dbContext.ContasReceber.AsNoTracking()
            .Where(c => c.PagadorId == pessoaId
                && c.GrupoReembolsoId != null
                && c.StatusContaId != StatusConta.LiquidadaId
                && c.StatusContaId != StatusConta.CanceladaId)
            .SumAsync(c => (decimal?)c.ValorLiquido, cancellationToken) ?? 0m;

        var ultimasContasPagar = await dbContext.ContasPagar.AsNoTracking()
            .Where(c => c.RecebedorId == pessoaId && c.StatusContaId != StatusConta.CanceladaId)
            .OrderByDescending(c => c.DataVencimento)
            .Take(10)
            .Select(c => new PessoaContaResumoResponse(
                c.Id, "ContaPagar", c.Descricao, c.DataVencimento, c.ValorLiquido,
                c.StatusContaId == StatusConta.LiquidadaId ? "Liquidada"
                    : c.DataVencimento < hoje ? "Vencida" : "Pendente"))
            .ToArrayAsync(cancellationToken);

        var ultimasContasReceber = await dbContext.ContasReceber.AsNoTracking()
            .Where(c => c.PagadorId == pessoaId && c.StatusContaId != StatusConta.CanceladaId)
            .OrderByDescending(c => c.DataVencimento)
            .Take(10)
            .Select(c => new PessoaContaResumoResponse(
                c.Id, "ContaReceber", c.Descricao, c.DataVencimento, c.ValorLiquido,
                c.StatusContaId == StatusConta.LiquidadaId ? "Recebida"
                    : c.DataVencimento < hoje ? "Vencida" : "Pendente"))
            .ToArrayAsync(cancellationToken);

        var contasRecentes = ultimasContasPagar
            .Concat(ultimasContasReceber)
            .OrderByDescending(c => c.DataVencimento)
            .Take(10)
            .ToArray();

        return new PessoaResumoFinanceiroResponse(
            pessoaId,
            pessoa.Nome,
            contasPagar?.TotalPendente ?? 0m,
            contasPagar?.TotalLiquidado ?? 0m,
            contasPagar?.TotalVencido ?? 0m,
            contasPagar?.QuantidadeTotal ?? 0,
            contasReceber?.TotalPendente ?? 0m,
            contasReceber?.TotalRecebido ?? 0m,
            contasReceber?.TotalVencido ?? 0m,
            contasReceber?.QuantidadeTotal ?? 0,
            reembolsoPendente,
            contasRecentes);
    }
}