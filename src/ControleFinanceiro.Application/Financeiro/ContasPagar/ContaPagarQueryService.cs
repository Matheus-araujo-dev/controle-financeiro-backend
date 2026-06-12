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
            var termo = $"%{query.Search.Trim()}%";
            consulta = consulta.Where(x =>
                EF.Functions.Like(x.Descricao, termo) ||
                (x.NumeroDocumento != null && EF.Functions.Like(x.NumeroDocumento, termo)) ||
                EF.Functions.Like(x.RecebedorNome, termo));
        }

        if (query.RecebedorId.HasValue)
        {
            consulta = consulta.Where(x => x.RecebedorId == query.RecebedorId.Value);
        }

        if (query.FormaPagamentoId.HasValue)
        {
            consulta = consulta.Where(x => x.FormaPagamentoId == query.FormaPagamentoId.Value);
        }

        if (query.DataVencimentoInicial.HasValue)
        {
            consulta = consulta.Where(x => x.DataVencimento >= query.DataVencimentoInicial.Value);
        }

        if (query.DataVencimentoFinal.HasValue)
        {
            consulta = consulta.Where(x => x.DataVencimento <= query.DataVencimentoFinal.Value);
        }

        var consultaBaseSummary = consulta;

        if (!string.IsNullOrWhiteSpace(query.StatusCodigo))
        {
            var statusCodigos = NormalizarStatusCodigos(query.StatusCodigo);
            consulta = consulta.Where(x => statusCodigos.Contains(x.StatusCodigo));
        }

        consulta = (query.SortBy ?? string.Empty).ToLowerInvariant() switch
        {
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

        var totalItems = await consulta.CountAsync(cancellationToken);
        var valorTotal = await consulta.SumAsync(x => (decimal?)x.ValorLiquido, cancellationToken) ?? 0m;
        
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var totalPendente = await consultaBaseSummary
            .Where(x => x.StatusCodigo == "PENDENTE" || x.StatusCodigo == "VENCIDA")
            .SumAsync(x => (decimal?)x.ValorLiquido, cancellationToken) ?? 0m;
        var totalVencendoHoje = await consultaBaseSummary
            .Where(x => x.DataVencimento == hoje && (x.StatusCodigo == "PENDENTE" || x.StatusCodigo == "VENCIDA"))
            .SumAsync(x => (decimal?)x.ValorLiquido, cancellationToken) ?? 0m;
        var totalLiquidado = await consultaBaseSummary
            .Where(x => x.StatusCodigo == "LIQUIDADA")
            .SumAsync(x => (decimal?)x.ValorLiquido, cancellationToken) ?? 0m;
        var items = (await consulta
                .ApplyPagination(query)
                .ToArrayAsync(cancellationToken))
            .Select(x => new ContaPagarResumoResponse(
                x.Id,
                x.NumeroDocumento,
                x.Descricao,
                x.RecebedorId,
                x.RecebedorNome,
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
                x.EhRecorrente))
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

    private static string[] NormalizarStatusCodigos(string statusCodigo)
    {
        return statusCodigo
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.ToUpperInvariant())
            .Distinct()
            .ToArray();
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
