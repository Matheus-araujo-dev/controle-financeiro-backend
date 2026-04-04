using ControleFinanceiro.Application.Common.Exceptions;
using ControleFinanceiro.Application.Common.Pagination;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Contracts.Financeiro.Faturas;
using ControleFinanceiro.Domain.Cadastros.Cartoes;
using ControleFinanceiro.Domain.Cadastros.ContasBancarias;
using ControleFinanceiro.Domain.Cadastros.Pessoas;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Financeiro.Faturas;

public sealed class FaturaCartaoAppService(IAppDbContext dbContext)
{
    public async Task<PagedResult<FaturaResumoResponse>> ListarAsync(
        FaturaListQueryRequest query,
        CancellationToken cancellationToken)
    {
        await SincronizarFaturasAsync(cancellationToken);

        var consulta =
            from fatura in dbContext.FaturasCartao.AsNoTracking()
            join cartao in dbContext.Cartoes.AsNoTracking() on fatura.CartaoId equals cartao.Id
            select new
            {
                fatura.Id,
                fatura.CartaoId,
                CartaoNome = cartao.Nome,
                fatura.Competencia,
                fatura.DataFechamento,
                fatura.DataVencimento,
                fatura.ValorTotal,
                fatura.DataPagamento,
                fatura.Status
            };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var termo = query.Search.Trim().ToLowerInvariant();
            consulta = consulta.Where(x =>
                x.CartaoNome.ToLower().Contains(termo) ||
                x.Competencia.Contains(termo));
        }

        if (query.CartaoId.HasValue)
        {
            consulta = consulta.Where(x => x.CartaoId == query.CartaoId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Competencia))
        {
            var competencia = query.Competencia.Trim();
            consulta = consulta.Where(x => x.Competencia == competencia);
        }

        if (!string.IsNullOrWhiteSpace(query.StatusCodigo))
        {
            var status = NormalizarStatus(query.StatusCodigo);
            consulta = consulta.Where(x => x.Status == status);
        }

        consulta = query.SortDirection == SortDirection.Desc
            ? consulta.OrderByDescending(x => x.DataVencimento).ThenByDescending(x => x.Competencia)
            : consulta.OrderBy(x => x.DataVencimento).ThenBy(x => x.Competencia);

        var totalItems = await consulta.CountAsync(cancellationToken);
        var selecionadas = await consulta
            .ApplyPagination(query)
            .ToArrayAsync(cancellationToken);

        var contagemItens = await CarregarQuantidadeItensAsync(
            selecionadas.Select(x => new FaturaLookupKey(x.CartaoId, x.Competencia)).ToArray(),
            cancellationToken);

        var items = selecionadas
            .Select(x =>
            {
                var status = MapearStatus(x.Status);
                return new FaturaResumoResponse(
                    x.Id,
                    x.CartaoId,
                    x.CartaoNome,
                    x.Competencia,
                    x.DataFechamento,
                    x.DataVencimento,
                    x.ValorTotal,
                    x.DataPagamento,
                    status.Codigo,
                    status.Nome,
                    contagemItens.GetValueOrDefault(new FaturaLookupKey(x.CartaoId, x.Competencia)));
            })
            .ToArray();

        return PagedResult<FaturaResumoResponse>.Create(items, query.Page, query.PageSize, totalItems);
    }

    public async Task<FaturaDetalheResponse?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        await SincronizarFaturasAsync(cancellationToken);

        var item = await (
            from fatura in dbContext.FaturasCartao.AsNoTracking()
            join cartao in dbContext.Cartoes.AsNoTracking() on fatura.CartaoId equals cartao.Id
            join contaBancaria in dbContext.ContasBancarias.AsNoTracking() on fatura.ContaBancariaPagamentoId equals contaBancaria.Id into contasJoin
            from contaBancaria in contasJoin.DefaultIfEmpty()
            where fatura.Id == id
            select new
            {
                fatura.Id,
                fatura.CartaoId,
                CartaoNome = cartao.Nome,
                fatura.Competencia,
                fatura.DataFechamento,
                fatura.DataVencimento,
                fatura.ValorTotal,
                fatura.DataPagamento,
                fatura.ContaBancariaPagamentoId,
                ContaBancariaPagamentoNome = contaBancaria != null ? contaBancaria.Nome : null,
                fatura.Status,
                fatura.Observacao,
                fatura.CreatedAtUtc,
                fatura.UpdatedAtUtc
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (item is null)
        {
            return null;
        }

        var itens = await ListarItensAsync(item.CartaoId, item.Competencia, cancellationToken);
        var status = MapearStatus(item.Status);

        return new FaturaDetalheResponse(
            item.Id,
            item.CartaoId,
            item.CartaoNome,
            item.Competencia,
            item.DataFechamento,
            item.DataVencimento,
            item.ValorTotal,
            item.DataPagamento,
            item.ContaBancariaPagamentoId,
            item.ContaBancariaPagamentoNome,
            status.Codigo,
            status.Nome,
            item.Observacao,
            itens,
            item.CreatedAtUtc,
            item.UpdatedAtUtc);
    }

    public async Task<FaturaDetalheResponse?> PagarAsync(
        Guid id,
        PagarFaturaRequest request,
        CancellationToken cancellationToken)
    {
        await SincronizarFaturasAsync(cancellationToken);

        var fatura = await dbContext.FaturasCartao.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (fatura is null)
        {
            return null;
        }

        if (!await dbContext.ContasBancarias.AnyAsync(x => x.Id == request.ContaBancariaPagamentoId, cancellationToken))
        {
            throw ValidationExceptionFactory.Create("ContaBancariaPagamentoId", "Conta bancaria de pagamento nao encontrada.");
        }

        if (fatura.Status == StatusFaturaCartao.Paga)
        {
            throw ValidationExceptionFactory.Create("Status", "Fatura ja foi paga.");
        }

        if (await dbContext.MovimentacoesFinanceiras.AnyAsync(x => x.FaturaCartaoId == fatura.Id, cancellationToken))
        {
            throw ValidationExceptionFactory.Create("Status", "Pagamento da fatura ja foi registrado.");
        }

        var cartao = await dbContext.Cartoes
            .AsNoTracking()
            .SingleAsync(x => x.Id == fatura.CartaoId, cancellationToken);

        var comprasCartao = await dbContext.ContasPagar
            .Where(x => x.CartaoId == fatura.CartaoId && x.StatusContaId != StatusConta.CanceladaId)
            .ToListAsync(cancellationToken);

        var itensDaFatura = comprasCartao
            .Where(x => FaturaCartaoCompetencia.Calcular(
                    x.DataEmissao,
                    cartao.DiaFechamentoFatura,
                    cartao.DiaVencimentoFatura).Competencia == fatura.Competencia)
            .ToArray();

        if (itensDaFatura.Length == 0)
        {
            throw ValidationExceptionFactory.Create("Fatura", "Nao ha itens para pagamento nesta fatura.");
        }

        try
        {
            fatura.Pagar(request.DataPagamento, request.ContaBancariaPagamentoId, request.Observacao);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            throw ValidationExceptionFactory.Create("Request", exception.Message);
        }

        foreach (var conta in itensDaFatura.Where(x => x.StatusContaId != StatusConta.LiquidadaId))
        {
            conta.Liquidar(request.DataPagamento, request.ContaBancariaPagamentoId, StatusConta.LiquidadaId);
        }

        dbContext.MovimentacoesFinanceiras.Add(
            MovimentacaoFinanceira.CriarPagamentoFatura(
                fatura.Id,
                request.ContaBancariaPagamentoId,
                request.DataPagamento,
                fatura.ValorTotal,
                StatusMovimentacao.EfetivadaId,
                request.Observacao ?? $"Pagamento da fatura {fatura.Competencia}"));

        await dbContext.SaveChangesAsync(cancellationToken);
        return await ObterPorIdAsync(id, cancellationToken);
    }

    private async Task SincronizarFaturasAsync(CancellationToken cancellationToken)
    {
        var cartoes = await dbContext.Cartoes
            .AsNoTracking()
            .Select(x => new CartaoProjection(x.Id, x.DiaFechamentoFatura, x.DiaVencimentoFatura))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var comprasCartao = await dbContext.ContasPagar
            .AsNoTracking()
            .Where(x => x.CartaoId.HasValue && x.StatusContaId != StatusConta.CanceladaId)
            .ToListAsync(cancellationToken);

        var grupos = comprasCartao
            .Select(conta =>
            {
                var cartao = cartoes[conta.CartaoId!.Value];
                var competencia = FaturaCartaoCompetencia.Calcular(
                    conta.DataEmissao,
                    cartao.DiaFechamentoFatura,
                    cartao.DiaVencimentoFatura);

                return new CompraCartaoProjection(
                    conta.Id,
                    conta.CartaoId.Value,
                    competencia.Competencia,
                    competencia.DataFechamento,
                    competencia.DataVencimento,
                    conta.ValorLiquido);
            })
            .GroupBy(x => new FaturaLookupKey(x.CartaoId, x.Competencia))
            .Select(group => new FaturaSyncProjection(
                group.Key.CartaoId,
                group.Key.Competencia,
                group.First().DataFechamento,
                group.First().DataVencimento,
                group.Sum(x => x.ValorLiquido)))
            .ToArray();

        var faturasExistentes = await dbContext.FaturasCartao
            .ToDictionaryAsync(x => new FaturaLookupKey(x.CartaoId, x.Competencia), cancellationToken);

        var houveMudanca = false;

        foreach (var grupo in grupos)
        {
            if (faturasExistentes.TryGetValue(new FaturaLookupKey(grupo.CartaoId, grupo.Competencia), out var existente))
            {
                if (existente.Status != StatusFaturaCartao.Paga &&
                    (existente.DataFechamento != grupo.DataFechamento ||
                     existente.DataVencimento != grupo.DataVencimento ||
                     existente.ValorTotal != grupo.ValorTotal))
                {
                    houveMudanca = true;
                }

                existente.AtualizarDadosGerados(grupo.DataFechamento, grupo.DataVencimento, grupo.ValorTotal);
            }
            else
            {
                dbContext.FaturasCartao.Add(FaturaCartao.Criar(
                    grupo.CartaoId,
                    grupo.Competencia,
                    grupo.DataFechamento,
                    grupo.DataVencimento,
                    grupo.ValorTotal,
                    null));
                houveMudanca = true;
            }
        }

        if (houveMudanca)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<IReadOnlyCollection<FaturaItemResponse>> ListarItensAsync(
        Guid cartaoId,
        string competencia,
        CancellationToken cancellationToken)
    {
        var cartao = await dbContext.Cartoes
            .AsNoTracking()
            .SingleAsync(x => x.Id == cartaoId, cancellationToken);

        var contas = await (
            from conta in dbContext.ContasPagar.AsNoTracking()
            join recebedor in dbContext.Pessoas.AsNoTracking() on conta.RecebedorId equals recebedor.Id
            join status in dbContext.StatusContas.AsNoTracking() on conta.StatusContaId equals status.Id
            where conta.CartaoId == cartaoId && conta.StatusContaId != StatusConta.CanceladaId
            select new
            {
                conta.Id,
                conta.Descricao,
                RecebedorNome = recebedor.Nome,
                conta.DataEmissao,
                conta.ValorLiquido,
                StatusCodigo = status.Codigo,
                conta.NumeroParcela,
                conta.QuantidadeParcelas
            })
            .ToArrayAsync(cancellationToken);

        return contas
            .Where(conta => FaturaCartaoCompetencia.Calcular(
                    conta.DataEmissao,
                    cartao.DiaFechamentoFatura,
                    cartao.DiaVencimentoFatura).Competencia == competencia)
            .OrderBy(conta => conta.DataEmissao)
            .ThenBy(conta => conta.NumeroParcela)
            .Select(conta => new FaturaItemResponse(
                conta.Id,
                conta.Descricao,
                conta.RecebedorNome,
                conta.DataEmissao,
                conta.ValorLiquido,
                conta.StatusCodigo,
                conta.NumeroParcela,
                conta.QuantidadeParcelas))
            .ToArray();
    }

    private async Task<Dictionary<FaturaLookupKey, int>> CarregarQuantidadeItensAsync(
        IReadOnlyCollection<FaturaLookupKey> chaves,
        CancellationToken cancellationToken)
    {
        if (chaves.Count == 0)
        {
            return [];
        }

        var cartoes = await dbContext.Cartoes
            .AsNoTracking()
            .Where(x => chaves.Select(chave => chave.CartaoId).Contains(x.Id))
            .Select(x => new CartaoProjection(x.Id, x.DiaFechamentoFatura, x.DiaVencimentoFatura))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var contas = await dbContext.ContasPagar
            .AsNoTracking()
            .Where(x => x.CartaoId.HasValue && x.StatusContaId != StatusConta.CanceladaId)
            .Select(x => new { x.CartaoId, x.DataEmissao })
            .ToArrayAsync(cancellationToken);

        return contas
            .Select(conta =>
            {
                var cartao = cartoes[conta.CartaoId!.Value];
                var competencia = FaturaCartaoCompetencia.Calcular(
                    conta.DataEmissao,
                    cartao.DiaFechamentoFatura,
                    cartao.DiaVencimentoFatura);
                return new FaturaLookupKey(conta.CartaoId.Value, competencia.Competencia);
            })
            .Where(chaves.Contains)
            .GroupBy(x => x)
            .ToDictionary(x => x.Key, x => x.Count());
    }

    private static StatusFaturaCartao NormalizarStatus(string statusCodigo)
    {
        return statusCodigo.Trim().ToUpperInvariant() switch
        {
            "ABERTA" => StatusFaturaCartao.Aberta,
            "PAGA" => StatusFaturaCartao.Paga,
            _ => throw ValidationExceptionFactory.Create("StatusCodigo", "Status de fatura invalido.")
        };
    }

    private static (string Codigo, string Nome) MapearStatus(StatusFaturaCartao status)
    {
        return status switch
        {
            StatusFaturaCartao.Aberta => ("ABERTA", "Aberta"),
            StatusFaturaCartao.Paga => ("PAGA", "Paga"),
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
    }

    private sealed record CartaoProjection(Guid Id, int DiaFechamentoFatura, int DiaVencimentoFatura);

    private sealed record CompraCartaoProjection(
        Guid ContaPagarId,
        Guid CartaoId,
        string Competencia,
        DateOnly DataFechamento,
        DateOnly DataVencimento,
        decimal ValorLiquido);

    private sealed record FaturaSyncProjection(
        Guid CartaoId,
        string Competencia,
        DateOnly DataFechamento,
        DateOnly DataVencimento,
        decimal ValorTotal);

    private sealed record FaturaLookupKey(Guid CartaoId, string Competencia);
}
