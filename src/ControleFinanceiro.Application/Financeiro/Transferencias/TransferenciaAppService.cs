using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Contracts.Financeiro.Transferencias;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Financeiro.Transferencias;

public sealed class TransferenciaAppService(IAppDbContext dbContext)
{
    public async Task<TransferenciaResumoResponse> CriarAsync(
        CriarTransferenciaRequest request,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.ContasBancarias.AnyAsync(x => x.Id == request.ContaBancariaOrigemId, cancellationToken))
            throw ValidationExceptionFactory.Create("ContaBancariaOrigemId", "Conta bancária de origem não encontrada.");

        if (!await dbContext.ContasBancarias.AnyAsync(x => x.Id == request.ContaBancariaDestinoId, cancellationToken))
            throw ValidationExceptionFactory.Create("ContaBancariaDestinoId", "Conta bancária de destino não encontrada.");

        if (request.ContaBancariaOrigemId == request.ContaBancariaDestinoId)
            throw ValidationExceptionFactory.Create("ContaBancariaDestinoId", "Conta de origem e destino não podem ser iguais.");

        var transferencia = Transferencia.Criar(
            request.ContaBancariaOrigemId,
            request.ContaBancariaDestinoId,
            request.Valor,
            request.DataTransferencia,
            request.Descricao);

        var observacao = string.IsNullOrWhiteSpace(request.Descricao)
            ? "Transferência entre contas"
            : request.Descricao.Trim();

        var saida = MovimentacaoFinanceira.CriarSaidaTransferencia(
            transferencia.Id,
            request.ContaBancariaOrigemId,
            request.DataTransferencia,
            request.Valor,
            observacao);

        var entrada = MovimentacaoFinanceira.CriarEntradaTransferencia(
            transferencia.Id,
            request.ContaBancariaDestinoId,
            request.DataTransferencia,
            request.Valor,
            observacao);

        dbContext.Transferencias.Add(transferencia);
        dbContext.MovimentacoesFinanceiras.Add(saida);
        dbContext.MovimentacoesFinanceiras.Add(entrada);

        await dbContext.SaveChangesAsync(cancellationToken);

        return await ProjetarAsync(transferencia.Id, cancellationToken)
            ?? throw new InvalidOperationException("Falha ao carregar transferência criada.");
    }

    public async Task<TransferenciaListResponse> ListarAsync(
        TransferenciaListQuery query,
        CancellationToken cancellationToken)
    {
        var q = dbContext.Transferencias.AsQueryable();

        if (query.ContaBancariaOrigemId.HasValue)
            q = q.Where(x => x.ContaBancariaOrigemId == query.ContaBancariaOrigemId.Value);

        if (query.ContaBancariaDestinoId.HasValue)
            q = q.Where(x => x.ContaBancariaDestinoId == query.ContaBancariaDestinoId.Value);

        if (query.DataInicial.HasValue)
            q = q.Where(x => x.DataTransferencia >= query.DataInicial.Value);

        if (query.DataFinal.HasValue)
            q = q.Where(x => x.DataTransferencia <= query.DataFinal.Value);

        if (query.Cancelada.HasValue)
            q = q.Where(x => x.Cancelada == query.Cancelada.Value);

        var total = await q.CountAsync(cancellationToken);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var page = Math.Max(1, query.Page);
        var totalPages = (int)Math.Ceiling(total / (double)pageSize);

        var ids = await q
            .OrderByDescending(x => x.DataTransferencia)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var items = new List<TransferenciaResumoResponse>(ids.Count);
        foreach (var id in ids)
        {
            var item = await ProjetarAsync(id, cancellationToken);
            if (item is not null) items.Add(item);
        }

        return new TransferenciaListResponse(items, page, pageSize, total, totalPages);
    }

    public async Task<TransferenciaResumoResponse?> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken)
        => await ProjetarAsync(id, cancellationToken);

    public async Task<TransferenciaResumoResponse?> CancelarAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var transferencia = await dbContext.Transferencias
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (transferencia is null) return null;

        if (transferencia.Cancelada)
            throw ValidationExceptionFactory.Create("Id", "Transferência já está cancelada.");

        transferencia.Cancelar();

        var movimentacoes = await dbContext.MovimentacoesFinanceiras
            .Where(x => x.TransferenciaId == id)
            .ToListAsync(cancellationToken);

        foreach (var mov in movimentacoes)
            mov.Cancelar(StatusMovimentacao.CanceladaId);

        await dbContext.SaveChangesAsync(cancellationToken);

        return await ProjetarAsync(id, cancellationToken);
    }

    private async Task<TransferenciaResumoResponse?> ProjetarAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await dbContext.Transferencias
            .Where(x => x.Id == id)
            .Join(dbContext.ContasBancarias,
                t => t.ContaBancariaOrigemId,
                o => o.Id,
                (t, o) => new { t, OrigemNome = o.Nome })
            .Join(dbContext.ContasBancarias,
                x => x.t.ContaBancariaDestinoId,
                d => d.Id,
                (x, d) => new TransferenciaResumoResponse(
                    x.t.Id,
                    x.t.ContaBancariaOrigemId,
                    x.OrigemNome,
                    x.t.ContaBancariaDestinoId,
                    d.Nome,
                    x.t.Valor,
                    x.t.DataTransferencia,
                    x.t.Descricao,
                    x.t.Cancelada,
                    x.t.CreatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
