using ControleFinanceiro.Application.Common.Cache;
using ControleFinanceiro.Application.Common.Pagination;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Contracts.Financeiro.Common;
using ControleFinanceiro.Contracts.Financeiro.ContasPagar;
using ControleFinanceiro.Contracts.Financeiro.ContasReceber;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Financeiro.ContasReceber;

public sealed class ContaReceberQueryService(IAppDbContext dbContext, ILookupCacheService lookupCache) : IContaReceberQueryService
{
    private readonly ILookupCacheService _lookupCache = lookupCache;

    public async Task<ContaReceberListResponse> ListarAsync(
        ContaReceberListQueryRequest query,
        CancellationToken cancellationToken)
    {
        var consulta =
            from conta in dbContext.ContasReceber.AsNoTracking()
            join pagador in dbContext.Pessoas.AsNoTracking() on conta.PagadorId equals pagador.Id
            join forma in dbContext.FormasPagamento.AsNoTracking() on conta.FormaPagamentoId equals forma.Id
            join status in dbContext.StatusContas.AsNoTracking() on conta.StatusContaId equals status.Id
            select new
            {
                conta.Id,
                conta.NumeroDocumento,
                conta.Descricao,
                conta.PagadorId,
                PagadorNome = pagador.Nome,
                conta.ResponsavelId,
                ResponsavelNome = dbContext.Pessoas
                    .Where(pessoa => pessoa.Id == conta.ResponsavelId)
                    .Select(pessoa => pessoa.Nome)
                    .FirstOrDefault(),
                conta.DataEmissao,
                conta.DataVencimento,
                conta.DataLiquidacao,
                conta.FormaPagamentoId,
                FormaPagamentoNome = forma.Nome,
                conta.ValorLiquido,
                StatusCodigo = status.Codigo,
                StatusNome = status.Nome,
                conta.QuantidadeParcelas,
                conta.NumeroParcela,
                conta.GrupoParcelamentoId,
                conta.EhRecorrente
            };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var termo = $"%{query.Search.Trim().ToLower()}%";
            consulta = consulta.Where(x =>
                EF.Functions.Like(x.Descricao.ToLower(), termo) ||
                (x.NumeroDocumento != null && EF.Functions.Like(x.NumeroDocumento.ToLower(), termo)) ||
                EF.Functions.Like(x.PagadorNome.ToLower(), termo));
        }

        if (!string.IsNullOrWhiteSpace(query.NumeroDocumento))
        {
            var termo = $"%{query.NumeroDocumento.Trim().ToLower()}%";
            consulta = consulta.Where(x => x.NumeroDocumento != null && EF.Functions.Like(x.NumeroDocumento.ToLower(), termo));
        }

        if (!string.IsNullOrWhiteSpace(query.Descricao))
        {
            var termo = $"%{query.Descricao.Trim().ToLower()}%";
            consulta = consulta.Where(x => EF.Functions.Like(x.Descricao.ToLower(), termo));
        }

        var pagadorIds = NormalizarIds(query.PagadorId, query.PagadorIds);
        if (pagadorIds.Length > 0)
            consulta = consulta.Where(x => pagadorIds.Contains(x.PagadorId));

        var responsavelIds = NormalizarIds(null, query.ResponsavelIds);
        if (responsavelIds.Length > 0)
            consulta = consulta.Where(x => x.ResponsavelId.HasValue && responsavelIds.Contains(x.ResponsavelId.Value));

        var formaPagamentoIds = NormalizarIds(query.FormaPagamentoId, query.FormaPagamentoIds);
        if (formaPagamentoIds.Length > 0)
            consulta = consulta.Where(x => formaPagamentoIds.Contains(x.FormaPagamentoId));

        if (query.DataVencimentoInicial.HasValue)
            consulta = consulta.Where(x => x.DataVencimento >= query.DataVencimentoInicial.Value);

        if (query.DataVencimentoFinal.HasValue)
            consulta = consulta.Where(x => x.DataVencimento <= query.DataVencimentoFinal.Value);

        if (query.DataEmissaoInicial.HasValue)
            consulta = consulta.Where(x => x.DataEmissao >= query.DataEmissaoInicial.Value);

        if (query.DataEmissaoFinal.HasValue)
            consulta = consulta.Where(x => x.DataEmissao <= query.DataEmissaoFinal.Value);

        if (query.ValorMinimo.HasValue)
            consulta = consulta.Where(x => x.ValorLiquido >= query.ValorMinimo.Value);

        if (query.ValorMaximo.HasValue)
            consulta = consulta.Where(x => x.ValorLiquido <= query.ValorMaximo.Value);

        var consultaBaseSummary = consulta;

        var statusCodigosFiltro = NormalizarStatusCodigos(query.StatusCodigo, query.StatusCodigos);
        if (statusCodigosFiltro.Length > 0)
        {
            var incluiVencida = statusCodigosFiltro.Contains("VENCIDA");
            var statusFiltrados = statusCodigosFiltro.Where(status => status != "VENCIDA").ToArray();

            if (incluiVencida)
            {
                consulta = consulta.Where(x =>
                    statusFiltrados.Contains(x.StatusCodigo) ||
                    x.StatusCodigo == "VENCIDA" ||
                    (x.StatusCodigo == "PENDENTE" && x.DataVencimento < DateOnly.FromDateTime(DateTime.Today)));
            }
            else if (statusFiltrados.Length > 0)
            {
                consulta = consulta.Where(x => statusFiltrados.Contains(x.StatusCodigo));
            }
        }

        if (query.EhRecorrente.HasValue)
            consulta = consulta.Where(x => x.EhRecorrente == query.EhRecorrente.Value);

        var consultaFiltrada = consulta;

        consulta = (query.SortBy ?? string.Empty).ToLowerInvariant() switch
        {
            "pagadornome" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.PagadorNome).ThenByDescending(x => x.DataVencimento)
                : consulta.OrderBy(x => x.PagadorNome).ThenBy(x => x.DataVencimento),
            "descricao" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.Descricao).ThenByDescending(x => x.DataVencimento)
                : consulta.OrderBy(x => x.Descricao).ThenBy(x => x.DataVencimento),
            "formapagamentonome" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.FormaPagamentoNome).ThenByDescending(x => x.DataVencimento)
                : consulta.OrderBy(x => x.FormaPagamentoNome).ThenBy(x => x.DataVencimento),
            "statuscodigo" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.StatusCodigo).ThenByDescending(x => x.DataVencimento)
                : consulta.OrderBy(x => x.StatusCodigo).ThenBy(x => x.DataVencimento),
            "valorliquido" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.ValorLiquido).ThenByDescending(x => x.DataVencimento)
                : consulta.OrderBy(x => x.ValorLiquido).ThenBy(x => x.DataVencimento),
            "datavencimento" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.DataVencimento).ThenByDescending(x => x.Descricao)
                : consulta.OrderBy(x => x.DataVencimento).ThenBy(x => x.Descricao),
            _ => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.DataVencimento).ThenByDescending(x => x.Descricao)
                : consulta.OrderBy(x => x.DataVencimento).ThenBy(x => x.Descricao)
        };

        var hoje = DateOnly.FromDateTime(DateTime.Today);

        var totais = await consultaFiltrada
            .GroupBy(_ => 1)
            .Select(g => new { Total = g.Count(), Valor = g.Sum(x => x.ValorLiquido) })
            .FirstOrDefaultAsync(cancellationToken);
        var totalItems = totais?.Total ?? 0;
        var valorTotal = totais?.Valor ?? 0m;

        var resumo = await consultaBaseSummary
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Pendente = g.Sum(x => x.StatusCodigo == "PENDENTE" || x.StatusCodigo == "VENCIDA" || x.StatusCodigo == "PARCIAL"
                    ? x.ValorLiquido : 0m),
                Vencido = g.Sum(x => x.DataVencimento < hoje && (x.StatusCodigo == "PENDENTE" || x.StatusCodigo == "VENCIDA" || x.StatusCodigo == "PARCIAL")
                    ? x.ValorLiquido : 0m),
                VencendoHoje = g.Sum(x => x.DataVencimento == hoje && (x.StatusCodigo == "PENDENTE" || x.StatusCodigo == "VENCIDA" || x.StatusCodigo == "PARCIAL")
                    ? x.ValorLiquido : 0m),
                Liquidado = g.Sum(x => x.StatusCodigo == "LIQUIDADA" ? x.ValorLiquido : 0m)
            })
            .FirstOrDefaultAsync(cancellationToken);
        var totalPendente = resumo?.Pendente ?? 0m;
        var totalVencido = resumo?.Vencido ?? 0m;
        var totalVencendoHoje = resumo?.VencendoHoje ?? 0m;
        var totalLiquidado = resumo?.Liquidado ?? 0m;

        var paginados = await consulta.ApplyPagination(query).ToArrayAsync(cancellationToken);

        var parcialIds = paginados.Where(x => x.StatusCodigo == "PARCIAL").Select(x => x.Id).ToArray();
        var valorPagoPorId = new Dictionary<Guid, decimal>();
        if (parcialIds.Length > 0)
        {
            valorPagoPorId = await dbContext.MovimentacoesFinanceiras
                .Where(m => m.ContaReceberId != null && parcialIds.Contains(m.ContaReceberId.Value) &&
                            m.Natureza == NaturezaMovimentacao.Realizada &&
                            m.StatusMovimentacaoId != StatusMovimentacao.CanceladaId)
                .GroupBy(m => m.ContaReceberId!.Value)
                .Select(g => new { Id = g.Key, Total = g.Sum(m => m.Valor) })
                .ToDictionaryAsync(x => x.Id, x => x.Total, cancellationToken);
        }

        var items = paginados
            .Select(x =>
            {
                var (statusCodigo, statusNome) = ResolverStatusEfetivo(x.StatusCodigo, x.StatusNome, x.DataVencimento, hoje);
                var valorPago = valorPagoPorId.TryGetValue(x.Id, out var vp) ? vp : (decimal?)null;
                return new ContaReceberResumoResponse(
                    x.Id, x.NumeroDocumento, x.Descricao, x.PagadorId, x.PagadorNome,
                    x.ResponsavelNome, x.DataEmissao, x.DataVencimento, x.DataLiquidacao,
                    x.FormaPagamentoId, x.FormaPagamentoNome, x.ValorLiquido, valorPago,
                    statusCodigo, statusNome, x.QuantidadeParcelas, x.NumeroParcela,
                    x.GrupoParcelamentoId, x.EhRecorrente);
            })
            .ToArray();

        var paged = PagedResult<ContaReceberResumoResponse>.Create(items, query.Page, query.PageSize, totalItems);
        return new ContaReceberListResponse(
            paged.Items, paged.Page, paged.PageSize, paged.TotalItems, paged.TotalPages,
            new ContaReceberListSummaryResponse(
                totalItems,
                decimal.Round(valorTotal, 2, MidpointRounding.AwayFromZero),
                decimal.Round(totalPendente, 2, MidpointRounding.AwayFromZero),
                decimal.Round(totalVencido, 2, MidpointRounding.AwayFromZero),
                decimal.Round(totalVencendoHoje, 2, MidpointRounding.AwayFromZero),
                decimal.Round(totalLiquidado, 2, MidpointRounding.AwayFromZero)));
    }

    public async Task<ContaReceberDetalheResponse?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasReceber
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return conta is null ? null : await MapearDetalheAsync(conta, cancellationToken);
    }

    internal async Task<ContaReceberDetalheResponse> MapearDetalheAsync(ContaReceber conta, CancellationToken cancellationToken)
    {
        var pagador = await dbContext.Pessoas.AsNoTracking().SingleAsync(x => x.Id == conta.PagadorId, cancellationToken);
        var responsavel = conta.ResponsavelId.HasValue
            ? await dbContext.Pessoas.AsNoTracking().SingleOrDefaultAsync(x => x.Id == conta.ResponsavelId.Value, cancellationToken)
            : null;
        var formaPagamento = await _lookupCache.GetFormaPagamentoByIdAsync(conta.FormaPagamentoId, cancellationToken)
            ?? throw new InvalidOperationException("FormaPagamento not found");
        var cartao = conta.CartaoId.HasValue
            ? await dbContext.Cartoes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == conta.CartaoId.Value, cancellationToken)
            : null;
        var contaBancaria = conta.ContaBancariaId.HasValue
            ? await dbContext.ContasBancarias.AsNoTracking().SingleOrDefaultAsync(x => x.Id == conta.ContaBancariaId.Value, cancellationToken)
            : null;
        var status = await _lookupCache.GetStatusContaByIdAsync(conta.StatusContaId, cancellationToken)
            ?? throw new InvalidOperationException("StatusConta not found");
        var regra = conta.RegraRecorrenciaId.HasValue
            ? await dbContext.RegrasRecorrencia.AsNoTracking().SingleOrDefaultAsync(x => x.Id == conta.RegraRecorrenciaId.Value, cancellationToken)
            : null;

        var rateios = await (
            from rateio in dbContext.RateiosContaGerencial.AsNoTracking()
            join contaGerencial in dbContext.ContasGerenciais.AsNoTracking() on rateio.ContaGerencialId equals contaGerencial.Id
            where rateio.ContaReceberId == conta.Id
            orderby contaGerencial.Descricao
            select new RateioResponse(
                rateio.Id, rateio.ContaGerencialId, contaGerencial.Codigo,
                contaGerencial.Descricao, rateio.Valor, rateio.Percentual))
            .ToArrayAsync(cancellationToken);

        var valorPago = conta.StatusContaId == StatusConta.ParcialId
            ? await dbContext.MovimentacoesFinanceiras
                .Where(m => m.ContaReceberId == conta.Id &&
                            m.Natureza == NaturezaMovimentacao.Realizada &&
                            m.StatusMovimentacaoId != StatusMovimentacao.CanceladaId)
                .SumAsync(m => (decimal?)m.Valor, cancellationToken)
            : (decimal?)null;

        return new ContaReceberDetalheResponse(
            conta.Id, conta.NumeroDocumento, conta.DataEmissao,
            conta.ResponsavelId, responsavel?.Nome,
            conta.PagadorId, pagador.Nome,
            conta.DataVencimento, conta.DataLiquidacao,
            conta.FormaPagamentoId, formaPagamento.Nome, formaPagamento.EhCartao, formaPagamento.BaixarAutomaticamente,
            conta.CartaoId, cartao?.Nome,
            conta.ContaBancariaId, contaBancaria?.Nome,
            conta.ValorOriginal, conta.ValorDesconto, conta.ValorJuros, conta.ValorMulta, conta.ValorLiquido,
            valorPago, conta.QuantidadeParcelas, conta.NumeroParcela, conta.GrupoParcelamentoId,
            conta.Descricao, conta.Observacao, status.Codigo, status.Nome,
            conta.EhRecorrente,
            ContaReceberSharedHelper.MapearOrigem(conta.Origem),
            ContaReceberSharedHelper.MapearRecorrencia(regra),
            rateios, conta.CreatedAtUtc, conta.UpdatedAtUtc);
    }

    private static Guid[] NormalizarIds(Guid? idSingular, IReadOnlyCollection<Guid>? ids)
    {
        if (ids is not null && ids.Count > 0) return ids.Distinct().ToArray();
        return idSingular.HasValue ? [idSingular.Value] : [];
    }

    private static string[] NormalizarStatusCodigos(string? statusCodigo, IReadOnlyCollection<string>? statusCodigos)
    {
        var valores = new List<string>();
        if (!string.IsNullOrWhiteSpace(statusCodigo))
            valores.AddRange(statusCodigo.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.ToUpperInvariant()));
        if (statusCodigos is not null)
            valores.AddRange(statusCodigos.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim().ToUpperInvariant()));
        return valores.Distinct().ToArray();
    }

    // O status "Vencida" não é gravado em tempo real (só pelo worker diário). Para a listagem nunca
    // exibir "Pendente" em conta já vencida — e ficar consistente com o filtro de status —, calcula-se
    // o status efetivo: PENDENTE com vencimento no passado é apresentado como VENCIDA.
    private static (string Codigo, string Nome) ResolverStatusEfetivo(
        string statusCodigo, string statusNome, DateOnly dataVencimento, DateOnly hoje) =>
        statusCodigo == "PENDENTE" && dataVencimento < hoje
            ? ("VENCIDA", "Vencida")
            : (statusCodigo, statusNome);
}
