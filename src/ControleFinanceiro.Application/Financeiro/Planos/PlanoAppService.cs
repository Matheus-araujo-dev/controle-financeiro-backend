using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Contracts.Financeiro.Planos;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Financeiro.Planos;

public sealed class PlanoAppService(IAppDbContext dbContext)
{
    public async Task<PlanoResumoResponse> CriarAsync(
        CriarPlanoRequest request,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.ContasBancarias.AnyAsync(x => x.Id == request.ContaBancariaCaixaId, cancellationToken))
            throw ValidationExceptionFactory.Create("ContaBancariaCaixaId", "Conta bancária não encontrada.");

        var plano = Plano.Criar(
            request.Nome,
            request.Descricao,
            request.ValorMensal,
            request.NumParcelas,
            request.ContaBancariaCaixaId);

        dbContext.Planos.Add(plano);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await ProjetarAsync(plano.Id, cancellationToken)
            ?? throw new InvalidOperationException("Falha ao carregar plano criado.");
    }

    public async Task<PlanoResumoResponse?> AtualizarAsync(
        Guid id,
        AtualizarPlanoRequest request,
        CancellationToken cancellationToken)
    {
        var plano = await dbContext.Planos
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (plano is null) return null;

        plano.Atualizar(request.Nome, request.Descricao, request.ValorMensal, request.NumParcelas);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await ProjetarAsync(id, cancellationToken);
    }

    public async Task<PlanoListResponse> ListarAsync(
        PlanoListQuery query,
        CancellationToken cancellationToken)
    {
        var q = dbContext.Planos.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(x => x.Nome.Contains(query.Search));

        if (query.ContaBancariaCaixaId.HasValue)
            q = q.Where(x => x.ContaBancariaCaixaId == query.ContaBancariaCaixaId.Value);

        if (query.Cancelado.HasValue)
            q = q.Where(x => x.Cancelado == query.Cancelado.Value);

        var total = await q.CountAsync(cancellationToken);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var page = Math.Max(1, query.Page);
        var totalPages = (int)Math.Ceiling(total / (double)pageSize);

        var ids = await q
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var items = new List<PlanoResumoResponse>(ids.Count);
        foreach (var id in ids)
        {
            var item = await ProjetarAsync(id, cancellationToken);
            if (item is not null) items.Add(item);
        }

        return new PlanoListResponse(items, page, pageSize, total, totalPages);
    }

    public async Task<PlanoResumoResponse?> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken)
        => await ProjetarAsync(id, cancellationToken);

    public async Task<PlanoResumoResponse?> AdiantarParcelaAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var plano = await dbContext.Planos
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (plano is null) return null;

        if (plano.Cancelado)
            throw ValidationExceptionFactory.Create("Id", "Plano cancelado não aceita novas parcelas.");

        if (plano.Concluido)
            throw ValidationExceptionFactory.Create("Id", "Plano já está concluído.");

        plano.AdiantarParcela();
        await dbContext.SaveChangesAsync(cancellationToken);

        return await ProjetarAsync(id, cancellationToken);
    }

    public async Task<PlanoResumoResponse?> RetirarDinheiroAsync(
        Guid id,
        RetirarDinheiroRequest request,
        CancellationToken cancellationToken)
    {
        var plano = await dbContext.Planos
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (plano is null) return null;

        if (plano.Cancelado)
            throw ValidationExceptionFactory.Create("Id", "Plano cancelado não aceita retiradas.");

        plano.RetirarDinheiro(request.Valor);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await ProjetarAsync(id, cancellationToken);
    }

    public async Task<PlanoResumoResponse?> CancelarAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var plano = await dbContext.Planos
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (plano is null) return null;

        if (plano.Cancelado)
            throw ValidationExceptionFactory.Create("Id", "Plano já está cancelado.");

        plano.Cancelar();
        await dbContext.SaveChangesAsync(cancellationToken);

        return await ProjetarAsync(id, cancellationToken);
    }

    private async Task<PlanoResumoResponse?> ProjetarAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await dbContext.Planos
            .Where(x => x.Id == id)
            .Join(dbContext.ContasBancarias,
                p => p.ContaBancariaCaixaId,
                c => c.Id,
                (p, c) => new PlanoResumoResponse(
                    p.Id,
                    p.Nome,
                    p.Descricao,
                    p.ValorMensal,
                    p.NumParcelas,
                    p.ContaBancariaCaixaId,
                    c.Nome,
                    p.ParcelasPagas,
                    p.TotalRetirado,
                    p.ValorMensal * p.NumParcelas,
                    (p.ValorMensal * p.ParcelasPagas) - p.TotalRetirado,
                    p.ParcelasPagas >= p.NumParcelas,
                    p.Cancelado,
                    p.CreatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
