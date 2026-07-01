using ControleFinanceiro.Application.Common.Cache;
using ControleFinanceiro.Application.Common.Exceptions;
using ControleFinanceiro.Application.Common.Pagination;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Contracts.Financeiro.Common;
using ControleFinanceiro.Contracts.Financeiro.ContasPagar;
using ControleFinanceiro.Domain.Cadastros.Cartoes;
using ControleFinanceiro.Domain.Cadastros.ContasBancarias;
using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;
using ControleFinanceiro.Domain.Cadastros.FormasPagamento;
using ControleFinanceiro.Domain.Cadastros.Pessoas;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;
using TipoPeriodicidadeRecorrenciaDomain = ControleFinanceiro.Domain.Financeiro.TipoPeriodicidadeRecorrencia;
using TipoDiaRecorrenciaDomain = ControleFinanceiro.Domain.Financeiro.TipoDiaRecorrencia;

namespace ControleFinanceiro.Application.Financeiro.ContasPagar;

public sealed class ContaPagarQueryService(IAppDbContext dbContext, ILookupCacheService lookupCache) : IContaPagarQueryService
{
    private readonly ILookupCacheService _lookupCache = lookupCache;

    public async Task<ContaPagarListResponse> ListarAsync(
        ContaPagarListQueryRequest query,
        CancellationToken cancellationToken)
    {
        var consulta =
            from conta in dbContext.ContasPagar.AsNoTracking()
            join recebedor in dbContext.Pessoas.AsNoTracking() on conta.RecebedorId equals recebedor.Id
            join forma in dbContext.FormasPagamento.AsNoTracking() on conta.FormaPagamentoId equals forma.Id
            join status in dbContext.StatusContas.AsNoTracking() on conta.StatusContaId equals status.Id
            where !conta.CartaoId.HasValue
            select new
            {
                conta.Id,
                conta.NumeroDocumento,
                conta.Descricao,
                conta.RecebedorId,
                RecebedorNome = recebedor.Nome,
                ResponsavelNome = dbContext.Pessoas
                    .Where(pessoa => pessoa.Id == conta.ResponsavelCompraId)
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
                EF.Functions.Like(x.RecebedorNome.ToLower(), termo));
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

        var recebedorIds = NormalizarIds(query.RecebedorId, query.RecebedorIds);
        if (recebedorIds.Length > 0)
        {
            consulta = consulta.Where(x => recebedorIds.Contains(x.RecebedorId));
        }

        var formaPagamentoIds = NormalizarIds(query.FormaPagamentoId, query.FormaPagamentoIds);
        if (formaPagamentoIds.Length > 0)
        {
            consulta = consulta.Where(x => formaPagamentoIds.Contains(x.FormaPagamentoId));
        }

        if (query.DataVencimentoInicial.HasValue)
        {
            consulta = consulta.Where(x => x.DataVencimento >= query.DataVencimentoInicial.Value);
        }

        if (query.DataVencimentoFinal.HasValue)
        {
            consulta = consulta.Where(x => x.DataVencimento <= query.DataVencimentoFinal.Value);
        }

        if (query.DataEmissaoInicial.HasValue)
        {
            consulta = consulta.Where(x => x.DataEmissao >= query.DataEmissaoInicial.Value);
        }

        if (query.DataEmissaoFinal.HasValue)
        {
            consulta = consulta.Where(x => x.DataEmissao <= query.DataEmissaoFinal.Value);
        }

        if (query.ValorMinimo.HasValue)
        {
            consulta = consulta.Where(x => x.ValorLiquido >= query.ValorMinimo.Value);
        }

        if (query.ValorMaximo.HasValue)
        {
            consulta = consulta.Where(x => x.ValorLiquido <= query.ValorMaximo.Value);
        }

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
        {
            consulta = consulta.Where(x => x.EhRecorrente == query.EhRecorrente.Value);
        }

        // Conjunto totalmente filtrado (inclui status/recorrência), antes da ordenação,
        // usado para os totais que respeitam todos os filtros e para a página.
        var consultaFiltrada = consulta;

        consulta = (query.SortBy ?? string.Empty).ToLowerInvariant() switch
        {
            "recebedornome" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.RecebedorNome).ThenByDescending(x => x.DataVencimento)
                : consulta.OrderBy(x => x.RecebedorNome).ThenBy(x => x.DataVencimento),
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

        // Totais que respeitam todos os filtros (inclusive status): 1 round-trip.
        var totais = await consultaFiltrada
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Valor = g.Sum(x => x.ValorLiquido)
            })
            .FirstOrDefaultAsync(cancellationToken);
        var totalItems = totais?.Total ?? 0;
        var valorTotal = totais?.Valor ?? 0m;

        // Breakdown por situação, ignorando o filtro de status (consultaBaseSummary): 1 round-trip.
        var resumo = await consultaBaseSummary
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Pendente = g.Sum(x => x.StatusCodigo == "PENDENTE" || x.StatusCodigo == "VENCIDA" || x.StatusCodigo == "PARCIAL"
                    ? x.ValorLiquido
                    : 0m),
                Vencido = g.Sum(x => x.DataVencimento < hoje && (x.StatusCodigo == "PENDENTE" || x.StatusCodigo == "VENCIDA" || x.StatusCodigo == "PARCIAL")
                    ? x.ValorLiquido
                    : 0m),
                VencendoHoje = g.Sum(x => x.DataVencimento == hoje && (x.StatusCodigo == "PENDENTE" || x.StatusCodigo == "VENCIDA" || x.StatusCodigo == "PARCIAL")
                    ? x.ValorLiquido
                    : 0m),
                Liquidado = g.Sum(x => x.StatusCodigo == "LIQUIDADA" ? x.ValorLiquido : 0m)
            })
            .FirstOrDefaultAsync(cancellationToken);
        var totalPendente = resumo?.Pendente ?? 0m;
        var totalVencido = resumo?.Vencido ?? 0m;
        var totalVencendoHoje = resumo?.VencendoHoje ?? 0m;
        var totalLiquidado = resumo?.Liquidado ?? 0m;

        var items = (await consulta
                .ApplyPagination(query)
                .ToArrayAsync(cancellationToken))
            .Select(x =>
            {
                var (statusCodigo, statusNome) = ResolverStatusEfetivo(x.StatusCodigo, x.StatusNome, x.DataVencimento, hoje);
                return new ContaPagarResumoResponse(
                    x.Id,
                    x.NumeroDocumento,
                    x.Descricao,
                    x.RecebedorId,
                    x.RecebedorNome,
                    x.ResponsavelNome,
                    x.DataEmissao,
                    x.DataVencimento,
                    x.DataLiquidacao,
                    x.FormaPagamentoId,
                    x.FormaPagamentoNome,
                    x.ValorLiquido,
                    statusCodigo,
                    statusNome,
                    x.QuantidadeParcelas,
                    x.NumeroParcela,
                    x.GrupoParcelamentoId,
                    x.EhRecorrente);
            })
            .ToArray();

        var paged = PagedResult<ContaPagarResumoResponse>.Create(items, query.Page, query.PageSize, totalItems);
        return new ContaPagarListResponse(
            paged.Items,
            paged.Page,
            paged.PageSize,
            paged.TotalItems,
            paged.TotalPages,
            new ContaPagarListSummaryResponse(
                totalItems,
                decimal.Round(valorTotal, 2, MidpointRounding.AwayFromZero),
                decimal.Round(totalPendente, 2, MidpointRounding.AwayFromZero),
                decimal.Round(totalVencido, 2, MidpointRounding.AwayFromZero),
                decimal.Round(totalVencendoHoje, 2, MidpointRounding.AwayFromZero),
                decimal.Round(totalLiquidado, 2, MidpointRounding.AwayFromZero)));
    }

    public async Task<ContaPagarDetalheResponse?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasPagar
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (conta is null)
        {
            return null;
        }

        var rateios = await (
            from rateio in dbContext.RateiosContaGerencial.AsNoTracking()
            join cg in dbContext.ContasGerenciais.AsNoTracking() on rateio.ContaGerencialId equals cg.Id
            where rateio.ContaPagarId == id
            orderby cg.Descricao
            select new RateioResponse(
                rateio.Id,
                rateio.ContaGerencialId,
                cg.Codigo,
                cg.Descricao,
                rateio.Valor,
                rateio.Percentual))
            .ToArrayAsync(cancellationToken);

        return await MapearDetalheAsync(conta, rateios, cancellationToken);
    }

    private static Guid[] NormalizarIds(Guid? idSingular, IReadOnlyCollection<Guid>? ids)
    {
        if (ids is not null && ids.Count > 0)
        {
            return ids.Distinct().ToArray();
        }

        return idSingular.HasValue ? [idSingular.Value] : [];
    }

    private static string[] NormalizarStatusCodigos(string? statusCodigo, IReadOnlyCollection<string>? statusCodigos)
    {
        var valores = new List<string>();

        if (!string.IsNullOrWhiteSpace(statusCodigo))
        {
            valores.AddRange(
                statusCodigo.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.ToUpperInvariant()));
        }

        if (statusCodigos is not null)
        {
            valores.AddRange(
                statusCodigos
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim().ToUpperInvariant()));
        }

        return valores
            .Distinct()
            .ToArray();
    }

    // O status "Vencida" não é gravado em tempo real (só pelo worker diário). Para a listagem nunca
    // exibir "Pendente" em conta já vencida — e ficar consistente com o filtro de status —, calcula-se
    // o status efetivo: PENDENTE com vencimento no passado é apresentado como VENCIDA.
    private static (string Codigo, string Nome) ResolverStatusEfetivo(
        string statusCodigo,
        string statusNome,
        DateOnly dataVencimento,
        DateOnly hoje)
    {
        return statusCodigo == "PENDENTE" && dataVencimento < hoje
            ? ("VENCIDA", "Vencida")
            : (statusCodigo, statusNome);
    }

    private async Task<ContaPagarDetalheResponse> MapearDetalheAsync(
        ContaPagar conta,
        RateioResponse[]? rateios,
        CancellationToken cancellationToken)
    {
        var recebedor = await dbContext.Pessoas.AsNoTracking().SingleAsync(x => x.Id == conta.RecebedorId, cancellationToken);
        var responsavel = conta.ResponsavelCompraId.HasValue
            ? await dbContext.Pessoas.AsNoTracking().SingleOrDefaultAsync(x => x.Id == conta.ResponsavelCompraId.Value, cancellationToken)
            : null;
        var formaPagamento = await _lookupCache.GetFormaPagamentoByIdAsync(conta.FormaPagamentoId, cancellationToken)
            ?? throw new InvalidOperationException("FormaPagamento not found");
        var cartao = conta.CartaoId.HasValue
            ? await dbContext.Cartoes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == conta.CartaoId.Value, cancellationToken)
            : null;
        var competenciaFatura = cartao is null
            ? null
            : FaturaCartaoCompetencia.CalcularPorDataVencimento(
                conta.DataVencimento,
                cartao.DiaFechamentoFatura,
                cartao.DiaVencimentoFatura);
        var contaBancaria = conta.ContaBancariaId.HasValue
            ? await dbContext.ContasBancarias.AsNoTracking().SingleOrDefaultAsync(x => x.Id == conta.ContaBancariaId.Value, cancellationToken)
            : null;
        var status = await _lookupCache.GetStatusContaByIdAsync(conta.StatusContaId, cancellationToken)
            ?? throw new InvalidOperationException("StatusConta not found");
        var regra = conta.RegraRecorrenciaId.HasValue
            ? await dbContext.RegrasRecorrencia.AsNoTracking().SingleOrDefaultAsync(x => x.Id == conta.RegraRecorrenciaId.Value, cancellationToken)
            : null;

        if (rateios is null)
        {
            rateios = await (
                from rateio in dbContext.RateiosContaGerencial.AsNoTracking()
                join contaGerencial in dbContext.ContasGerenciais.AsNoTracking() on rateio.ContaGerencialId equals contaGerencial.Id
                where rateio.ContaPagarId == conta.Id
                orderby contaGerencial.Descricao
                select new RateioResponse(
                    rateio.Id,
                    rateio.ContaGerencialId,
                    contaGerencial.Codigo,
                    contaGerencial.Descricao,
                    rateio.Valor,
                    rateio.Percentual))
                .ToArrayAsync(cancellationToken);
        }

        return new ContaPagarDetalheResponse(
            conta.Id,
            conta.NumeroDocumento,
            conta.DataEmissao,
            conta.ResponsavelCompraId,
            responsavel?.Nome,
            conta.RecebedorId,
            recebedor.Nome,
            conta.DataVencimento,
            conta.DataLiquidacao,
            conta.FormaPagamentoId,
            formaPagamento.Nome,
            formaPagamento.EhCartao,
            formaPagamento.BaixarAutomaticamente,
            conta.CartaoId,
            cartao?.Nome,
            conta.ContaBancariaId,
            contaBancaria?.Nome,
            conta.ValorOriginal,
            conta.ValorDesconto,
            conta.ValorJuros,
            conta.ValorMulta,
            conta.ValorLiquido,
            conta.QuantidadeParcelas,
            conta.NumeroParcela,
            conta.GrupoParcelamentoId,
            conta.OrigemCompraPlanejadaId,
            conta.Descricao,
            conta.Observacao,
            status.Codigo,
            status.Nome,
            conta.EhRecorrente,
            MapearOrigem(conta.Origem),
            MapearRecorrencia(regra),
            competenciaFatura?.Competencia,
            competenciaFatura?.DataFechamento,
            competenciaFatura?.DataVencimento,
            rateios,
            conta.CreatedAtUtc,
            conta.UpdatedAtUtc);
    }

    private static LancamentoOrigem MapearOrigem(OrigemLancamento origem)
    {
        return origem switch
        {
            OrigemLancamento.Manual => LancamentoOrigem.Manual,
            OrigemLancamento.Recorrencia => LancamentoOrigem.Recorrencia,
            OrigemLancamento.Importacao => LancamentoOrigem.Importacao,
            _ => throw new ArgumentOutOfRangeException(nameof(origem))
        };
    }

    private static RecorrenciaResponse? MapearRecorrencia(RegraRecorrencia? regra)
    {
        return regra is null
            ? null
            : new RecorrenciaResponse(
                regra.Id,
                MapearTipoPeriodicidadeContrato(regra.TipoPeriodicidade),
                MapearTipoDiaContrato(regra.TipoDia),
                regra.DiaOrdemMensal,
                regra.DataInicio,
                regra.DataFim,
                regra.Ativa,
                regra.PermiteEdicaoOcorrenciaIndividual,
                regra.Observacao);
    }

    private static Contracts.Financeiro.Common.TipoPeriodicidadeRecorrencia MapearTipoPeriodicidadeContrato(TipoPeriodicidadeRecorrenciaDomain tipo)
    {
        return tipo switch
        {
            TipoPeriodicidadeRecorrenciaDomain.Mensal => Contracts.Financeiro.Common.TipoPeriodicidadeRecorrencia.Mensal,
            _ => throw new ArgumentOutOfRangeException(nameof(tipo))
        };
    }

    public async Task<CursorPagedResult<ContaPagarResumoResponse>> ListarCursorAsync(
        ContaPagarCursorQueryRequest query,
        CancellationToken cancellationToken)
    {
        var consultaBase =
            from conta in dbContext.ContasPagar.AsNoTracking()
            join recebedor in dbContext.Pessoas.AsNoTracking() on conta.RecebedorId equals recebedor.Id
            join forma in dbContext.FormasPagamento.AsNoTracking() on conta.FormaPagamentoId equals forma.Id
            join status in dbContext.StatusContas.AsNoTracking() on conta.StatusContaId equals status.Id
            where !conta.CartaoId.HasValue
            select new
            {
                conta.Id,
                conta.NumeroDocumento,
                conta.Descricao,
                conta.RecebedorId,
                RecebedorNome = recebedor.Nome,
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
            consultaBase = consultaBase.Where(x =>
                EF.Functions.Like(x.Descricao.ToLower(), termo) ||
                EF.Functions.Like(x.RecebedorNome.ToLower(), termo));
        }

        if (query.RecebedorId.HasValue)
            consultaBase = consultaBase.Where(x => x.RecebedorId == query.RecebedorId.Value);

        if (query.DataVencimentoInicial.HasValue)
            consultaBase = consultaBase.Where(x => x.DataVencimento >= query.DataVencimentoInicial.Value);

        if (query.DataVencimentoFinal.HasValue)
            consultaBase = consultaBase.Where(x => x.DataVencimento <= query.DataVencimentoFinal.Value);

        if (query.StatusCodigos is { Length: > 0 })
            consultaBase = consultaBase.Where(x => query.StatusCodigos.Contains(x.StatusCodigo));

        // Aplica cursor: WHERE (DataVencimento, Id) > (cursorDate, cursorId)
        var cursor = CursorPaginationHelper.DecodeCursor(query.AfterCursor);
        if (cursor is { } c)
        {
            var cursorDate = DateOnly.Parse(c.SortValue);
            var cursorId = c.Id;
            consultaBase = consultaBase.Where(x =>
                x.DataVencimento > cursorDate ||
                (x.DataVencimento == cursorDate && x.Id.CompareTo(cursorId) > 0));
        }

        consultaBase = consultaBase
            .OrderBy(x => x.DataVencimento)
            .ThenBy(x => x.Id);

        var pageSize = query.NormalizedPageSize;
        var rows = await consultaBase.Take(pageSize + 1).ToListAsync(cancellationToken);

        var items = rows.Select(x => new ContaPagarResumoResponse(
            x.Id,
            x.NumeroDocumento,
            x.Descricao,
            x.RecebedorId,
            x.RecebedorNome,
            null,
            x.DataEmissao,
            x.DataVencimento,
            x.DataLiquidacao,
            x.FormaPagamentoId,
            x.FormaPagamentoNome,
            x.ValorLiquido,
            x.StatusCodigo,
            x.StatusNome,
            x.QuantidadeParcelas,
            x.NumeroParcela,
            x.GrupoParcelamentoId,
            x.EhRecorrente)).ToList();

        return CursorPaginationHelper.FromList(
            items,
            pageSize,
            item => item.DataVencimento.ToString("O"),
            item => item.Id);
    }

    private static Contracts.Financeiro.Common.TipoDiaRecorrencia MapearTipoDiaContrato(TipoDiaRecorrenciaDomain tipo)
    {
        return tipo switch
        {
            TipoDiaRecorrenciaDomain.DiaFixo => Contracts.Financeiro.Common.TipoDiaRecorrencia.DiaFixo,
            TipoDiaRecorrenciaDomain.DiaUtil => Contracts.Financeiro.Common.TipoDiaRecorrencia.DiaUtil,
            _ => throw new ArgumentOutOfRangeException(nameof(tipo))
        };
    }
}
