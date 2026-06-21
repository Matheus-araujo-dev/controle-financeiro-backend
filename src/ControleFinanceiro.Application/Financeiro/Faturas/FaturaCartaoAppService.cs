using ControleFinanceiro.Application.Common.Exceptions;
using ControleFinanceiro.Application.Common.Extensions;
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
    public Task SincronizarAsync(CancellationToken cancellationToken)
    {
        return SincronizarFaturasAsync(cancellationToken);
    }

    public async Task<FaturaListResponse> ListarAsync(
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
            var termo = $"%{query.Search.Trim()}%";
            consulta = consulta.Where(x =>
                EF.Functions.Like(x.CartaoNome, termo) ||
                EF.Functions.Like(x.Competencia, termo));
        }

        var cartaoIds = query.CartaoIds?
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToHashSet() ?? [];

        if (query.CartaoId.HasValue && query.CartaoId.Value != Guid.Empty)
        {
            cartaoIds.Add(query.CartaoId.Value);
        }

        if (cartaoIds.Count > 0)
        {
            consulta = consulta.Where(x => cartaoIds.Contains(x.CartaoId));
        }

        if (!string.IsNullOrWhiteSpace(query.Competencia))
        {
            var competencia = query.Competencia.Trim();
            consulta = consulta.Where(x => x.Competencia == competencia);
        }

        var statusSelecionados = query.StatusCodigos?
            .Where(status => !string.IsNullOrWhiteSpace(status))
            .Select(NormalizarStatus)
            .Distinct()
            .ToHashSet() ?? [];

        if (!string.IsNullOrWhiteSpace(query.StatusCodigo))
        {
            statusSelecionados.Add(NormalizarStatus(query.StatusCodigo));
        }

        if (statusSelecionados.Count > 0)
        {
            consulta = consulta.Where(x => statusSelecionados.Contains(x.Status));
        }

        if (query.DataVencimentoInicial.HasValue)
        {
            consulta = consulta.Where(x => x.DataVencimento >= query.DataVencimentoInicial.Value);
        }

        if (query.DataVencimentoFinal.HasValue)
        {
            consulta = consulta.Where(x => x.DataVencimento <= query.DataVencimentoFinal.Value);
        }

        if (query.DataFechamentoInicial.HasValue)
        {
            consulta = consulta.Where(x => x.DataFechamento >= query.DataFechamentoInicial.Value);
        }

        if (query.DataFechamentoFinal.HasValue)
        {
            consulta = consulta.Where(x => x.DataFechamento <= query.DataFechamentoFinal.Value);
        }

        var totais = await consulta
            .GroupBy(_ => 1)
            .Select(g => new { Total = g.Count(), Valor = g.Sum(x => x.ValorTotal) })
            .FirstOrDefaultAsync(cancellationToken);
        var totalItems = totais?.Total ?? 0;
        var valorTotal = totais?.Valor ?? 0m;
        var totalPorCartaoBruto = await consulta
            .GroupBy(x => new { x.CartaoId, x.CartaoNome })
            .Select(group => new
            {
                group.Key.CartaoId,
                group.Key.CartaoNome,
                QuantidadeFaturas = group.Count(),
                ValorTotal = group.Sum(x => x.ValorTotal)
            })
            .ToArrayAsync(cancellationToken);

        var totalPorCartao = totalPorCartaoBruto
            .Select(item => new FaturaAgrupamentoResumoResponse(
                item.CartaoId.ToString(),
                item.CartaoNome,
                item.QuantidadeFaturas,
                decimal.Round(item.ValorTotal, 2, MidpointRounding.AwayFromZero)))
            .OrderByDescending(item => item.ValorTotal)
            .ThenBy(item => item.Label)
            .ToArray();

        var totalPorCompetenciaBruto = await consulta
            .GroupBy(x => x.Competencia)
            .Select(group => new
            {
                Competencia = group.Key,
                QuantidadeFaturas = group.Count(),
                ValorTotal = group.Sum(x => x.ValorTotal)
            })
            .ToArrayAsync(cancellationToken);

        var totalPorCompetencia = totalPorCompetenciaBruto
            .Select(item => new FaturaAgrupamentoResumoResponse(
                item.Competencia,
                item.Competencia,
                item.QuantidadeFaturas,
                decimal.Round(item.ValorTotal, 2, MidpointRounding.AwayFromZero)))
            .OrderByDescending(item => item.Chave)
            .ToArray();

        consulta = (query.SortBy ?? string.Empty).ToLowerInvariant() switch
        {
            "cartaonome" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.CartaoNome).ThenByDescending(x => x.Competencia)
                : consulta.OrderBy(x => x.CartaoNome).ThenBy(x => x.Competencia),
            "competencia" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.Competencia).ThenByDescending(x => x.CartaoNome)
                : consulta.OrderBy(x => x.Competencia).ThenBy(x => x.CartaoNome),
            "datafechamento" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.DataFechamento).ThenByDescending(x => x.CartaoNome)
                : consulta.OrderBy(x => x.DataFechamento).ThenBy(x => x.CartaoNome),
            "datavencimento" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.DataVencimento).ThenByDescending(x => x.CartaoNome)
                : consulta.OrderBy(x => x.DataVencimento).ThenBy(x => x.CartaoNome),
            "valortotal" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.ValorTotal).ThenByDescending(x => x.Competencia)
                : consulta.OrderBy(x => x.ValorTotal).ThenBy(x => x.Competencia),
            "statuscodigo" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.Status).ThenByDescending(x => x.DataVencimento)
                : consulta.OrderBy(x => x.Status).ThenBy(x => x.DataVencimento),
            "quantidadeitens" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.ValorTotal).ThenByDescending(x => x.DataVencimento)
                : consulta.OrderBy(x => x.ValorTotal).ThenBy(x => x.DataVencimento),
            _ => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.DataVencimento).ThenByDescending(x => x.Competencia)
                : consulta.OrderBy(x => x.DataVencimento).ThenBy(x => x.Competencia)
        };

        var selecionadas = await consulta
            .ApplyPagination(query with { SortBy = null })
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

        var paged = PagedResult<FaturaResumoResponse>.Create(items, query.Page, query.PageSize, totalItems);
        return new FaturaListResponse(
            paged.Items,
            paged.Page,
            paged.PageSize,
            paged.TotalItems,
            paged.TotalPages,
            new FaturaListSummaryResponse(
                totalItems,
                decimal.Round(valorTotal, 2, MidpointRounding.AwayFromZero),
                totalPorCartao,
                totalPorCompetencia));
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
            throw ValidationExceptionFactory.Create("ContaBancariaPagamentoId", "Conta bancária de pagamento não encontrada.");
        }

        if (fatura.Status == StatusFaturaCartao.Paga)
        {
            throw ValidationExceptionFactory.Create("Status", "Fatura ja foi paga.");
        }

        if (await dbContext.MovimentacoesFinanceiras.AnyAsync(
                x => x.FaturaCartaoId == fatura.Id &&
                     x.StatusMovimentacaoId != StatusMovimentacao.CanceladaId,
                cancellationToken))
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
            .Where(x => FaturaCartaoCompetencia.CalcularPorDataVencimento(
                    x.DataVencimento,
                    cartao.DiaFechamentoFatura,
                    cartao.DiaVencimentoFatura).Competencia == fatura.Competencia)
            .ToArray();

        var contaPagarFatura = await dbContext.ContasPagar
            .SingleOrDefaultAsync(
                x => x.FaturaCartaoId == fatura.Id && !x.CartaoId.HasValue && x.StatusContaId != StatusConta.CanceladaId,
                cancellationToken);

        if (itensDaFatura.Length == 0)
        {
            throw ValidationExceptionFactory.Create("Fatura", "Não há itens para pagamento nesta fatura.");
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

        if (contaPagarFatura is not null && contaPagarFatura.StatusContaId != StatusConta.LiquidadaId)
        {
            contaPagarFatura.Liquidar(request.DataPagamento, request.ContaBancariaPagamentoId, StatusConta.LiquidadaId);
            dbContext.MovimentacoesFinanceiras.Add(
                MovimentacaoFinanceira.CriarLiquidacaoContaPagar(
                    contaPagarFatura.Id,
                    request.ContaBancariaPagamentoId,
                    request.DataPagamento,
                    contaPagarFatura.ValorLiquido,
                    StatusMovimentacao.EfetivadaId,
                    request.Observacao ?? $"Pagamento da fatura {fatura.Competencia}",
                    fatura.Id));
        }
        else
        {
            dbContext.MovimentacoesFinanceiras.Add(
                MovimentacaoFinanceira.CriarPagamentoFatura(
                    fatura.Id,
                    request.ContaBancariaPagamentoId,
                    request.DataPagamento,
                    fatura.ValorTotal,
                    StatusMovimentacao.EfetivadaId,
                    request.Observacao ?? $"Pagamento da fatura {fatura.Competencia}"));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await ObterPorIdAsync(id, cancellationToken);
    }

    public async Task<FaturaDetalheResponse?> EstornarAsync(Guid id, CancellationToken cancellationToken)
    {
        await SincronizarFaturasAsync(cancellationToken);

        var fatura = await dbContext.FaturasCartao.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (fatura is null)
        {
            return null;
        }

        try
        {
            fatura.ReabrirPagamento();
        }
        catch (InvalidOperationException exception)
        {
            throw ValidationExceptionFactory.Create("Status", exception.Message);
        }

        var movimentos = await dbContext.MovimentacoesFinanceiras
            .Where(x => x.FaturaCartaoId == fatura.Id && x.StatusMovimentacaoId != StatusMovimentacao.CanceladaId)
            .ToListAsync(cancellationToken);

        foreach (var movimento in movimentos)
        {
            movimento.Cancelar(StatusMovimentacao.CanceladaId);
        }

        var contaPagarFatura = await dbContext.ContasPagar
            .SingleOrDefaultAsync(
                x => x.FaturaCartaoId == fatura.Id && !x.CartaoId.HasValue && x.StatusContaId != StatusConta.CanceladaId,
                cancellationToken);

        if (contaPagarFatura is not null && contaPagarFatura.StatusContaId == StatusConta.LiquidadaId)
        {
            contaPagarFatura.Estornar(StatusConta.PendenteId);
        }

        var cartao = await dbContext.Cartoes
            .AsNoTracking()
            .SingleAsync(x => x.Id == fatura.CartaoId, cancellationToken);

        var comprasCartao = await dbContext.ContasPagar
            .Where(x => x.CartaoId == fatura.CartaoId && x.StatusContaId != StatusConta.CanceladaId)
            .ToListAsync(cancellationToken);

        var itensDaFatura = comprasCartao
            .Where(x => FaturaCartaoCompetencia.CalcularPorDataVencimento(
                    x.DataVencimento,
                    cartao.DiaFechamentoFatura,
                    cartao.DiaVencimentoFatura).Competencia == fatura.Competencia)
            .Where(x => x.StatusContaId == StatusConta.LiquidadaId)
            .ToArray();

        foreach (var item in itensDaFatura)
        {
            // Compras de cartão voltam para "Em fatura": seguem pertencendo à fatura reaberta.
            item.Estornar(StatusConta.EmFaturaId);
        }

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
            .Where(x => x.CartaoId.HasValue && x.StatusContaId != StatusConta.CanceladaId)
            .ToListAsync(cancellationToken);

        var grupos = comprasCartao
            .Select(conta =>
            {
                var cartao = cartoes[conta.CartaoId!.Value];
                var competencia = FaturaCartaoCompetencia.CalcularPorDataVencimento(
                    conta.DataVencimento,
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
        var hoje = DateOnly.FromDateTime(DateTime.Today);

        var gruposPorChave = grupos.ToDictionary(x => new FaturaLookupKey(x.CartaoId, x.Competencia));

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

                // Fatura já fechada: alteração nos dias do cartão não pode re-datar
                // competências passadas — apenas o valor continua sincronizado.
                if (existente.EstaFechada(hoje))
                {
                    existente.AtualizarValorTotal(grupo.ValorTotal);
                }
                else
                {
                    existente.AtualizarDadosGerados(grupo.DataFechamento, grupo.DataVencimento, grupo.ValorTotal);
                }
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

        foreach (var faturaExistente in faturasExistentes)
        {
            if (gruposPorChave.ContainsKey(faturaExistente.Key))
            {
                continue;
            }

            if (faturaExistente.Value.Status == StatusFaturaCartao.Paga)
            {
                continue;
            }

            // Fatura sem nenhuma compra restante (ex.: importação re-materializada)
            // é removida mesmo se já fechada — órfã não representa obrigação real.
            dbContext.FaturasCartao.Remove(faturaExistente.Value);
            houveMudanca = true;
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
                conta.DataVencimento,
                conta.ValorLiquido,
                StatusCodigo = status.Codigo,
                conta.NumeroParcela,
                conta.QuantidadeParcelas
            })
            .ToArrayAsync(cancellationToken);

        return contas
            .Where(conta => FaturaCartaoCompetencia.CalcularPorDataVencimento(
                    conta.DataVencimento,
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

        var cartaoIds = chaves.Select(chave => chave.CartaoId).Distinct().ToArray();

        var cartoes = await dbContext.Cartoes
            .AsNoTracking()
            .WhereIn(x => x.Id, cartaoIds)
            .Select(x => new CartaoProjection(x.Id, x.DiaFechamentoFatura, x.DiaVencimentoFatura))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var contas = await dbContext.ContasPagar
            .AsNoTracking()
            .Where(x => x.CartaoId.HasValue && x.StatusContaId != StatusConta.CanceladaId)
            .WhereIn(x => x.CartaoId!.Value, cartaoIds)
            .Select(x => new { x.CartaoId, x.DataVencimento })
            .ToArrayAsync(cancellationToken);

        return contas
            .Select(conta =>
            {
                var cartao = cartoes[conta.CartaoId!.Value];
                var competencia = FaturaCartaoCompetencia.CalcularPorDataVencimento(
                    conta.DataVencimento,
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
            _ => throw ValidationExceptionFactory.Create("StatusCodigo", "Status de fatura inválido.")
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
