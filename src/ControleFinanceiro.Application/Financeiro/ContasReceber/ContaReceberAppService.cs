using System.Text.Json;
using ControleFinanceiro.Application.Common.Cache;
using ControleFinanceiro.Application.Common.Exceptions;
using ControleFinanceiro.Application.Common.Pagination;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Application.Financeiro.Recorrencias;
using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Contracts.Financeiro.Common;
using ControleFinanceiro.Contracts.Financeiro.ContasPagar;
using ControleFinanceiro.Contracts.Financeiro.ContasReceber;
using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;
using ControleFinanceiro.Domain.Events;
using ControleFinanceiro.Domain.Financeiro;
using ControleFinanceiro.Domain.Financeiro.Events;
using Microsoft.EntityFrameworkCore;
using TipoPeriodicidadeRecorrenciaContract = ControleFinanceiro.Contracts.Financeiro.Common.TipoPeriodicidadeRecorrencia;
using TipoPeriodicidadeRecorrenciaDomain = ControleFinanceiro.Domain.Financeiro.TipoPeriodicidadeRecorrencia;
using TipoDiaRecorrenciaContract = ControleFinanceiro.Contracts.Financeiro.Common.TipoDiaRecorrencia;
using TipoDiaRecorrenciaDomain = ControleFinanceiro.Domain.Financeiro.TipoDiaRecorrencia;

namespace ControleFinanceiro.Application.Financeiro.ContasReceber;

public sealed class ContaReceberAppService(
    IAppDbContext dbContext,
    IDomainEventDispatcher eventDispatcher,
    ILookupCacheService lookupCache)
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
            var termo = $"%{query.Search.Trim()}%";
            consulta = consulta.Where(x =>
                EF.Functions.Like(x.Descricao, termo) ||
                (x.NumeroDocumento != null && EF.Functions.Like(x.NumeroDocumento, termo)) ||
                EF.Functions.Like(x.PagadorNome, termo));
        }

        if (!string.IsNullOrWhiteSpace(query.NumeroDocumento))
        {
            var termo = $"%{query.NumeroDocumento.Trim()}%";
            consulta = consulta.Where(x => x.NumeroDocumento != null && EF.Functions.Like(x.NumeroDocumento, termo));
        }

        if (!string.IsNullOrWhiteSpace(query.Descricao))
        {
            var termo = $"%{query.Descricao.Trim()}%";
            consulta = consulta.Where(x => EF.Functions.Like(x.Descricao, termo));
        }

        var pagadorIds = NormalizarIds(query.PagadorId, query.PagadorIds);
        if (pagadorIds.Length > 0)
        {
            consulta = consulta.Where(x => pagadorIds.Contains(x.PagadorId));
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

        // Conjunto totalmente filtrado (inclui status), antes da ordenação, para os totais e a página.
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
                return new ContaReceberResumoResponse(
                    x.Id,
                    x.NumeroDocumento,
                    x.Descricao,
                    x.PagadorId,
                    x.PagadorNome,
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

        var paged = PagedResult<ContaReceberResumoResponse>.Create(items, query.Page, query.PageSize, totalItems);
        return new ContaReceberListResponse(
            paged.Items,
            paged.Page,
            paged.PageSize,
            paged.TotalItems,
            paged.TotalPages,
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

    public async Task<ContaReceberDetalheResponse> CriarAsync(CriarContaReceberRequest request, CancellationToken cancellationToken)
    {
        ValidarRecorrencia(request.DataEmissao, request.Recorrencia, request.QuantidadeParcelas);

        var liquidarNaCriacao = await ValidarCriacaoOuAtualizacaoAsync(
            request.PagadorId,
            request.ResponsavelId,
            request.FormaPagamentoId,
            request.CartaoId,
            request.ContaBancariaId,
            request.DataLiquidacao,
            request.QuantidadeParcelas,
            request.Rateios,
            cancellationToken);

        RegraRecorrencia? regra = null;
        if (request.Recorrencia is not null)
        {
            regra = CriarRegraRecorrencia(request, request.Recorrencia);
            dbContext.RegrasRecorrencia.Add(regra);
        }

        var contas = ContaReceber.CriarParcelas(
            request.NumeroDocumento,
            request.DataEmissao,
            request.ResponsavelId,
            request.PagadorId,
            request.DataVencimento,
            request.FormaPagamentoId,
            request.CartaoId,
            request.ContaBancariaId,
            request.ValorOriginal,
            request.ValorDesconto,
            request.ValorJuros,
            request.ValorMulta,
            request.QuantidadeParcelas,
            request.Descricao,
            request.Observacao,
            StatusConta.PendenteId,
            regra is not null,
            regra?.Id,
            OrigemLancamento.Manual,
            ConverterRateios(request.Rateios));

        dbContext.ContasReceber.AddRange(contas);
        dbContext.RateiosContaGerencial.AddRange(contas.SelectMany(x => x.Rateios));

        if (liquidarNaCriacao)
        {
            dbContext.MovimentacoesFinanceiras.AddRange(AplicarLiquidacaoAutomatica(contas, request.DataLiquidacao, request.ContaBancariaId!.Value));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return await MapearDetalheAsync(contas.First(), cancellationToken);
    }

    public async Task<ContaReceberDetalheResponse?> AtualizarAsync(
        Guid id,
        AtualizarContaReceberRequest request,
        CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasReceber.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (conta is null)
        {
            return null;
        }

        if (request.QuantidadeParcelas != conta.QuantidadeParcelas)
        {
            throw ValidationExceptionFactory.Create("QuantidadeParcelas", "Não é permitido alterar o parcelamento na edição.");
        }

        ValidarRecorrencia(request.DataEmissao, request.Recorrencia, request.QuantidadeParcelas);
        Guid? regraRecorrenciaCriadaId = null;
        if (!conta.RegraRecorrenciaId.HasValue && request.Recorrencia is not null)
        {
            var regra = CriarRegraRecorrencia(request, request.Recorrencia);
            dbContext.RegrasRecorrencia.Add(regra);
            regraRecorrenciaCriadaId = regra.Id;
        }

        if (conta.RegraRecorrenciaId.HasValue)
        {
            var regra = await dbContext.RegrasRecorrencia
                .SingleAsync(x => x.Id == conta.RegraRecorrenciaId.Value, cancellationToken);

            if (!regra.PermiteEdicaoOcorrenciaIndividual)
            {
                throw ValidationExceptionFactory.Create("Recorrencia", "A regra atual não permite edição pontual da ocorrência.");
            }

            if (request.Recorrencia is not null)
            {
                var dataInicioRecorrencia = ResolveDataInicioRecorrencia(request.DataEmissao, request.Recorrencia);
                var dataFimRecorrencia = ResolveDataFimRecorrencia(request.Recorrencia);

                regra.Atualizar(
                    MapearTipoPeriodicidadeDominio(request.Recorrencia.TipoPeriodicidade),
                    MapearTipoDiaDominio(request.Recorrencia.TipoDia),
                    request.Recorrencia.DiaOrdemMensal,
                    dataInicioRecorrencia,
                    dataFimRecorrencia,
                    request.Recorrencia.PermiteEdicaoOcorrenciaIndividual,
                    request.Recorrencia.Observacao,
                    SerializarTemplate(request));
            }
        }

        var liquidarNaCriacao = await ValidarCriacaoOuAtualizacaoAsync(
            request.PagadorId,
            request.ResponsavelId,
            request.FormaPagamentoId,
            request.CartaoId,
            request.ContaBancariaId,
            request.DataLiquidacao,
            request.QuantidadeParcelas,
            request.Rateios,
            cancellationToken);

        AtualizarContaExistente(conta, request);
        if (regraRecorrenciaCriadaId.HasValue)
        {
            conta.VincularRecorrencia(regraRecorrenciaCriadaId.Value);
        }

        await SincronizarRateiosContaAsync(conta, cancellationToken);

        if (liquidarNaCriacao &&
            !await dbContext.MovimentacoesFinanceiras.AnyAsync(x => x.ContaReceberId == conta.Id, cancellationToken))
        {
            dbContext.MovimentacoesFinanceiras.AddRange(
                AplicarLiquidacaoAutomatica([conta], request.DataLiquidacao, request.ContaBancariaId!.Value));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await MapearDetalheAsync(conta, cancellationToken);
    }

    public async Task<ContaReceberDetalheResponse?> AlterarFuturasAsync(
        Guid id,
        AtualizarContaReceberRequest request,
        CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasReceber.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (conta is null)
        {
            return null;
        }

        var regra = await ObterRegraRecorrenciaObrigatoriaAsync(conta, cancellationToken);
        ValidarRecorrencia(request.DataEmissao, request.Recorrencia, request.QuantidadeParcelas);

        var recorrencia = request.Recorrencia ?? new RecorrenciaConfigRequest(
            MapearTipoPeriodicidadeContrato(regra.TipoPeriodicidade),
            MapearTipoDiaContrato(regra.TipoDia),
            regra.DiaOrdemMensal,
            regra.DataInicio,
            regra.DataFim,
            regra.PermiteEdicaoOcorrenciaIndividual,
            regra.Observacao);

        await ValidarCriacaoOuAtualizacaoAsync(
            request.PagadorId,
            request.ResponsavelId,
            request.FormaPagamentoId,
            request.CartaoId,
            request.ContaBancariaId,
            request.DataLiquidacao,
            request.QuantidadeParcelas,
            request.Rateios,
            cancellationToken);

        var dataInicioRecorrencia = ResolveDataInicioRecorrencia(request.DataEmissao, recorrencia);
        var dataFimRecorrencia = ResolveDataFimRecorrencia(recorrencia);

        regra.Atualizar(
            MapearTipoPeriodicidadeDominio(recorrencia.TipoPeriodicidade),
            MapearTipoDiaDominio(recorrencia.TipoDia),
            recorrencia.DiaOrdemMensal,
            dataInicioRecorrencia,
            dataFimRecorrencia,
            recorrencia.PermiteEdicaoOcorrenciaIndividual,
            recorrencia.Observacao,
            SerializarTemplate(request));

        var contasFuturas = await dbContext.ContasReceber
            .Where(x =>
                x.RegraRecorrenciaId == regra.Id &&
                x.DataVencimento >= conta.DataVencimento &&
                x.StatusContaId != StatusConta.LiquidadaId &&
                x.StatusContaId != StatusConta.CanceladaId)
            .OrderBy(x => x.DataVencimento)
            .ToListAsync(cancellationToken);

        foreach (var contaFutura in contasFuturas)
        {
            var mesOffset = RecorrenciaDateHelper.CalculateMonthOffset(conta.DataVencimento, contaFutura.DataVencimento);
            var requestAjustado = AjustarRequestParaMes(request, mesOffset);

            AtualizarContaExistente(contaFutura, requestAjustado);
            await SincronizarRateiosContaAsync(contaFutura, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await MapearDetalheAsync(conta, cancellationToken);
    }

    public async Task<ContaReceberDetalheResponse?> GerarOcorrenciasAsync(
        Guid id,
        GerarOcorrenciasRecorrenciaRequest request,
        CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasReceber.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (conta is null)
        {
            return null;
        }

        var regra = await ObterRegraRecorrenciaObrigatoriaAsync(conta, cancellationToken);

        if (!regra.Ativa)
        {
            throw ValidationExceptionFactory.Create("Recorrencia", "A recorrência está pausada ou encerrada.");
        }

        var datasExistentes = await dbContext.ContasReceber
            .Where(x => x.RegraRecorrenciaId == regra.Id)
            .Select(x => x.DataVencimento)
            .ToArrayAsync(cancellationToken);

        var datasPendentes = regra.CalcularDatasPendentes(datasExistentes, request.AteData);
        var template = DesserializarTemplate(regra.TemplateJson);

        var novasContas = datasPendentes
            .Select(dataVencimento => CriarOcorrenciaRecorrente(template, regra.Id, dataVencimento))
            .ToArray();

        dbContext.ContasReceber.AddRange(novasContas);
        dbContext.RateiosContaGerencial.AddRange(novasContas.SelectMany(x => x.Rateios));

        await dbContext.SaveChangesAsync(cancellationToken);
        return await MapearDetalheAsync(conta, cancellationToken);
    }

    public async Task<ContaReceberDetalheResponse?> PausarRecorrenciaAsync(Guid id, CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasReceber.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (conta is null)
        {
            return null;
        }

        var regra = await ObterRegraRecorrenciaObrigatoriaAsync(conta, cancellationToken);
        regra.Pausar();

        await dbContext.SaveChangesAsync(cancellationToken);
        return await MapearDetalheAsync(conta, cancellationToken);
    }

    public async Task<ContaReceberDetalheResponse?> EncerrarRecorrenciaAsync(
        Guid id,
        EncerrarRecorrenciaRequest request,
        CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasReceber.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (conta is null)
        {
            return null;
        }

        var regra = await ObterRegraRecorrenciaObrigatoriaAsync(conta, cancellationToken);
        regra.Encerrar(request.DataFim);

        var contasPosteriores = await dbContext.ContasReceber
            .Where(x =>
                x.RegraRecorrenciaId == regra.Id &&
                x.DataVencimento > request.DataFim &&
                x.StatusContaId != StatusConta.LiquidadaId &&
                x.StatusContaId != StatusConta.CanceladaId)
            .ToListAsync(cancellationToken);

        foreach (var contaPosterior in contasPosteriores)
        {
            contaPosterior.Cancelar(StatusConta.CanceladaId);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await MapearDetalheAsync(conta, cancellationToken);
    }

    public async Task<ContaReceberDetalheResponse?> LiquidarAsync(
        Guid id,
        LiquidarContaReceberRequest request,
        CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasReceber.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (conta is null)
        {
            return null;
        }

        if (conta.StatusContaId == StatusConta.LiquidadaId)
        {
            throw ValidationExceptionFactory.Create("Status", "Conta já está liquidada.");
        }

        if (!await dbContext.ContasBancarias.AnyAsync(x => x.Id == request.ContaBancariaId, cancellationToken))
        {
            throw ValidationExceptionFactory.Create("ContaBancariaId", "Conta bancária não encontrada.");
        }

        if (request.FormaPagamentoId.HasValue &&
            await _lookupCache.GetFormaPagamentoByIdAsync(request.FormaPagamentoId.Value, cancellationToken) is null)
        {
            throw ValidationExceptionFactory.Create("FormaPagamentoId", "Forma de pagamento não encontrada.");
        }

        var saldoJaLiquidado = await CalcularSaldoLiquidadoAsync(conta.Id, cancellationToken);
        var statusFinal = StatusConta.LiquidadaId;
        var valorMovimentacao = conta.ValorLiquido;

        var deveAtualizarValor = request.ValorLiquidacao > conta.ValorLiquido || request.AtualizarValorConta;
        var valorReferenciaConta = conta.ValorLiquido;

        if (deveAtualizarValor)
        {
            if (saldoJaLiquidado > 0)
            {
                throw ValidationExceptionFactory.Create(
                    "ValorLiquidacao",
                    "Conta com liquidações parciais já registradas não pode ter o valor atualizado.");
            }

            var novosRateios = await RecalcularRateiosAsync(conta.Id, request.ValorLiquidacao, cancellationToken);
            conta.AtualizarValorLiquido(request.ValorLiquidacao, novosRateios);
            valorReferenciaConta = request.ValorLiquidacao;

            if (conta.RegraRecorrenciaId.HasValue)
            {
                await AtualizarTemplateRecorrenciaAsync(conta.RegraRecorrenciaId.Value, request.ValorLiquidacao, novosRateios, cancellationToken);
            }
        }

        var saldoFinal = saldoJaLiquidado + request.ValorLiquidacao;
        statusFinal = saldoFinal < valorReferenciaConta ? StatusConta.ParcialId : StatusConta.LiquidadaId;
        valorMovimentacao = request.ValorLiquidacao;

        conta.Liquidar(request.DataLiquidacao, request.ContaBancariaId, statusFinal);
        dbContext.MovimentacoesFinanceiras.Add(
            MovimentacaoFinanceira.CriarLiquidacaoContaReceber(
                conta.Id,
                request.ContaBancariaId,
                request.DataLiquidacao,
                valorMovimentacao,
                StatusMovimentacao.EfetivadaId,
                conta.Descricao));

        if (statusFinal == StatusConta.LiquidadaId)
        {
            await eventDispatcher.DispatchAsync(
                new ContaReceberRecebidaEvent(
                    conta.Id,
                    conta.NumeroDocumento,
                    conta.PagadorId,
                    conta.Descricao,
                    conta.ValorLiquido,
                    request.DataLiquidacao,
                    request.ContaBancariaId),
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await MapearDetalheAsync(conta, cancellationToken);
    }

    private async Task<decimal> CalcularSaldoLiquidadoAsync(Guid contaId, CancellationToken cancellationToken)
    {
        return await dbContext.MovimentacoesFinanceiras
            .Where(x =>
                x.ContaReceberId == contaId &&
                x.Natureza == NaturezaMovimentacao.Realizada &&
                x.StatusMovimentacaoId != StatusMovimentacao.CanceladaId)
            .SumAsync(x => (decimal?)x.Valor, cancellationToken) ?? 0m;
    }

    private async Task<IReadOnlyCollection<RateioPlano>> RecalcularRateiosAsync(
        Guid contaId,
        decimal novoValorLiquido,
        CancellationToken cancellationToken)
    {
        var rateiosOriginais = await (
            from rateio in dbContext.RateiosContaGerencial.AsNoTracking()
            join contaGerencial in dbContext.ContasGerenciais.AsNoTracking() on rateio.ContaGerencialId equals contaGerencial.Id
            where rateio.ContaReceberId == contaId
            orderby contaGerencial.Descricao
            select new RateioPlano(rateio.ContaGerencialId, rateio.Valor))
            .ToArrayAsync(cancellationToken);

        if (rateiosOriginais.Length == 0)
        {
            throw ValidationExceptionFactory.Create("Rateios", "Ao menos um rateio é obrigatório.");
        }

        if (rateiosOriginais.Length == 1)
        {
            return [RateioPlano.Create(rateiosOriginais[0].ContaGerencialId, novoValorLiquido)];
        }

        var totalOriginal = rateiosOriginais.Sum(x => x.Valor);
        if (totalOriginal <= 0)
        {
            throw ValidationExceptionFactory.Create("Rateios", "Valor base de rateio inválido.");
        }

        var planos = new List<RateioPlano>(rateiosOriginais.Length);
        decimal acumulado = 0m;

        for (var index = 0; index < rateiosOriginais.Length - 1; index++)
        {
            var rateio = rateiosOriginais[index];
            var valorDistribuido = decimal.Round(
                novoValorLiquido * (rateio.Valor / totalOriginal),
                2,
                MidpointRounding.AwayFromZero);

            acumulado += valorDistribuido;
            planos.Add(RateioPlano.Create(rateio.ContaGerencialId, valorDistribuido));
        }

        var ultimo = rateiosOriginais[^1];
        planos.Add(RateioPlano.Create(ultimo.ContaGerencialId, decimal.Round(novoValorLiquido - acumulado, 2, MidpointRounding.AwayFromZero)));
        return planos;
    }

    private async Task AtualizarTemplateRecorrenciaAsync(
        Guid regraRecorrenciaId,
        decimal novoValorLiquido,
        IReadOnlyCollection<RateioPlano> novosRateios,
        CancellationToken cancellationToken)
    {
        var regra = await dbContext.RegrasRecorrencia.SingleAsync(x => x.Id == regraRecorrenciaId, cancellationToken);
        var template = DesserializarTemplate(regra.TemplateJson);
        var novoTemplate = template with
        {
            ValorOriginal = decimal.Round(novoValorLiquido + template.ValorDesconto - template.ValorJuros - template.ValorMulta, 2, MidpointRounding.AwayFromZero),
            Rateios = novosRateios.Select(rateio => new RateioRecorrenciaTemplate(rateio.ContaGerencialId, rateio.Valor)).ToArray()
        };

        regra.Atualizar(
            regra.TipoPeriodicidade,
            regra.TipoDia,
            regra.DiaOrdemMensal,
            regra.DataInicio,
            regra.DataFim,
            regra.PermiteEdicaoOcorrenciaIndividual,
            regra.Observacao,
            JsonSerializer.Serialize(novoTemplate));
    }

    public async Task<ContaReceberDetalheResponse?> EstornarAsync(Guid id, CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasReceber.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (conta is null)
        {
            return null;
        }

        try
        {
            conta.Estornar(StatusConta.PendenteId);
        }
        catch (InvalidOperationException exception)
        {
            throw ConverterParaValidacao(exception);
        }

        var movimentos = await dbContext.MovimentacoesFinanceiras
            .Where(x =>
                x.ContaReceberId == conta.Id &&
                x.Natureza == NaturezaMovimentacao.Realizada &&
                x.StatusMovimentacaoId != StatusMovimentacao.CanceladaId)
            .ToListAsync(cancellationToken);

        foreach (var movimento in movimentos)
        {
            movimento.Cancelar(StatusMovimentacao.CanceladaId);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await MapearDetalheAsync(conta, cancellationToken);
    }

    public async Task<ContaReceberDetalheResponse?> CancelarAsync(Guid id, CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasReceber.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (conta is null)
        {
            return null;
        }

        try
        {
            conta.Cancelar(StatusConta.CanceladaId);
        }
        catch (InvalidOperationException exception)
        {
            throw ConverterParaValidacao(exception);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await MapearDetalheAsync(conta, cancellationToken);
    }

    private async Task<ContaReceberDetalheResponse> MapearDetalheAsync(ContaReceber conta, CancellationToken cancellationToken)
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
                rateio.Id,
                rateio.ContaGerencialId,
                contaGerencial.Codigo,
                contaGerencial.Descricao,
                rateio.Valor,
                rateio.Percentual))
            .ToArrayAsync(cancellationToken);

        return new ContaReceberDetalheResponse(
            conta.Id,
            conta.NumeroDocumento,
            conta.DataEmissao,
            conta.ResponsavelId,
            responsavel?.Nome,
            conta.PagadorId,
            pagador.Nome,
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
            conta.Descricao,
            conta.Observacao,
            status.Codigo,
            status.Nome,
            conta.EhRecorrente,
            MapearOrigem(conta.Origem),
            MapearRecorrencia(regra),
            rateios,
            conta.CreatedAtUtc,
            conta.UpdatedAtUtc);
    }

    private async Task<bool> ValidarCriacaoOuAtualizacaoAsync(
        Guid pagadorId,
        Guid? responsavelId,
        Guid formaPagamentoId,
        Guid? cartaoId,
        Guid? contaBancariaId,
        DateOnly? dataLiquidacao,
        int quantidadeParcelas,
        IReadOnlyCollection<RateioRequest> rateios,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Pessoas.AnyAsync(x => x.Id == pagadorId, cancellationToken))
        {
            throw ValidationExceptionFactory.Create("PagadorId", "Pagador não encontrado.");
        }

        if (responsavelId.HasValue &&
            !await dbContext.Pessoas.AnyAsync(x => x.Id == responsavelId.Value, cancellationToken))
        {
            throw ValidationExceptionFactory.Create("ResponsavelId", "Responsável não encontrado.");
        }

        var formaPagamento = await _lookupCache.GetFormaPagamentoByIdAsync(formaPagamentoId, cancellationToken);

        if (formaPagamento is null)
        {
            throw ValidationExceptionFactory.Create("FormaPagamentoId", "Forma de pagamento não encontrada.");
        }

        if (quantidadeParcelas < 1)
        {
            throw ValidationExceptionFactory.Create("QuantidadeParcelas", "Quantidade de parcelas deve ser maior que zero.");
        }

        if (cartaoId.HasValue &&
            !await dbContext.Cartoes.AnyAsync(x => x.Id == cartaoId.Value, cancellationToken))
        {
            throw ValidationExceptionFactory.Create("CartaoId", "Cartão não encontrado.");
        }

        if (contaBancariaId.HasValue &&
            !await dbContext.ContasBancarias.AnyAsync(x => x.Id == contaBancariaId.Value, cancellationToken))
        {
            throw ValidationExceptionFactory.Create("ContaBancariaId", "Conta bancária não encontrada.");
        }

        await ContaGerencialLancamentoValidator.ValidarContasLancaveisPorTipoAsync(
            dbContext,
            rateios.Select(x => x.ContaGerencialId).ToArray(),
            TipoContaGerencial.Receita,
            "Rateios",
            "Uma ou mais contas gerenciais não foram encontradas.",
            "Somente contas gerenciais filhas podem ser usadas em rateios.",
            "Contas a receber aceitam apenas contas gerenciais de receita.",
            cancellationToken);

        if (!formaPagamento.BaixarAutomaticamente && dataLiquidacao.HasValue)
        {
            throw ValidationExceptionFactory.Create("DataLiquidacao", "Data de liquidação só pode ser informada com baixa automática.");
        }

        if (formaPagamento.BaixarAutomaticamente && !contaBancariaId.HasValue)
        {
            throw ValidationExceptionFactory.Create("ContaBancariaId", "Conta bancária é obrigatória para baixa automática.");
        }

        return formaPagamento.BaixarAutomaticamente;
    }

    private static IReadOnlyCollection<RateioPlano> ConverterRateios(IReadOnlyCollection<RateioRequest> rateios)
    {
        try
        {
            return rateios.Select(x => RateioPlano.Create(x.ContaGerencialId, x.Valor)).ToArray();
        }
        catch (ArgumentException exception)
        {
            throw ValidationExceptionFactory.Create("Rateios", exception.Message);
        }
    }

    private static IReadOnlyCollection<MovimentacaoFinanceira> AplicarLiquidacaoAutomatica(
        IReadOnlyCollection<ContaReceber> contas,
        DateOnly? dataLiquidacao,
        Guid contaBancariaId)
    {
        var movimentos = new List<MovimentacaoFinanceira>(contas.Count);

        foreach (var conta in contas)
        {
            var dataMovimentacao = (dataLiquidacao ?? conta.DataEmissao).AddMonths(conta.NumeroParcela - 1);
            conta.Liquidar(dataMovimentacao, contaBancariaId, StatusConta.LiquidadaId);
            movimentos.Add(MovimentacaoFinanceira.CriarLiquidacaoContaReceber(
                conta.Id,
                contaBancariaId,
                dataMovimentacao,
                conta.ValorLiquido,
                StatusMovimentacao.EfetivadaId,
                conta.Descricao));
        }

        return movimentos;
    }

    private static ApplicationValidationException ConverterParaValidacao(Exception exception)
    {
        return ValidationExceptionFactory.Create("Request", exception.Message);
    }

    private static TipoPeriodicidadeRecorrenciaDomain MapearTipoPeriodicidadeDominio(TipoPeriodicidadeRecorrenciaContract tipo)
    {
        return tipo switch
        {
            TipoPeriodicidadeRecorrenciaContract.Mensal => TipoPeriodicidadeRecorrenciaDomain.Mensal,
            _ => throw ValidationExceptionFactory.Create("Recorrencia.TipoPeriodicidade", "Tipo de periodicidade de recorrência inválido.")
        };
    }

    private static TipoPeriodicidadeRecorrenciaContract MapearTipoPeriodicidadeContrato(TipoPeriodicidadeRecorrenciaDomain tipo)
    {
        return tipo switch
        {
            TipoPeriodicidadeRecorrenciaDomain.Mensal => TipoPeriodicidadeRecorrenciaContract.Mensal,
            _ => throw new ArgumentOutOfRangeException(nameof(tipo))
        };
    }

    private static TipoDiaRecorrenciaDomain MapearTipoDiaDominio(TipoDiaRecorrenciaContract tipo)
    {
        return tipo switch
        {
            TipoDiaRecorrenciaContract.DiaFixo => TipoDiaRecorrenciaDomain.DiaFixo,
            TipoDiaRecorrenciaContract.DiaUtil => TipoDiaRecorrenciaDomain.DiaUtil,
            _ => throw ValidationExceptionFactory.Create("Recorrencia.TipoDia", "Tipo de dia de recorrência inválido.")
        };
    }

    private static TipoDiaRecorrenciaContract MapearTipoDiaContrato(TipoDiaRecorrenciaDomain tipo)
    {
        return tipo switch
        {
            TipoDiaRecorrenciaDomain.DiaFixo => TipoDiaRecorrenciaContract.DiaFixo,
            TipoDiaRecorrenciaDomain.DiaUtil => TipoDiaRecorrenciaContract.DiaUtil,
            _ => throw new ArgumentOutOfRangeException(nameof(tipo))
        };
    }

    private static void ValidarRecorrencia(
        DateOnly dataEmissao,
        RecorrenciaConfigRequest? recorrencia,
        int quantidadeParcelas)
    {
        if (recorrencia is not null && quantidadeParcelas != 1)
        {
            throw ValidationExceptionFactory.Create("QuantidadeParcelas", "Recorrência inicial não pode ser combinada com parcelamento.");
        }

        if (recorrencia is null)
        {
            return;
        }

        var dataInicio = ResolveDataInicioRecorrencia(dataEmissao, recorrencia);
        var dataFim = ResolveDataFimRecorrencia(recorrencia);

        if (dataFim.HasValue && dataFim.Value < dataInicio)
        {
            throw ValidationExceptionFactory.Create("Recorrencia.DataFim", "Data fim deve ser maior ou igual à primeira ocorrência da série.");
        }
    }

    private RegraRecorrencia CriarRegraRecorrencia(CriarContaReceberRequest request, RecorrenciaConfigRequest recorrencia)
    {
        var tipoDia = MapearTipoDiaDominio(recorrencia.TipoDia);
        var dataInicio = ResolveDataInicioRecorrencia(request.DataEmissao, recorrencia);
        var dataFim = ResolveDataFimRecorrencia(recorrencia);

        return RegraRecorrencia.Criar(
            TipoLancamentoRecorrencia.ContaReceber,
            MapearTipoPeriodicidadeDominio(recorrencia.TipoPeriodicidade),
            tipoDia,
            recorrencia.DiaOrdemMensal,
            dataInicio,
            dataFim,
            recorrencia.PermiteEdicaoOcorrenciaIndividual,
            recorrencia.Observacao,
            SerializarTemplate(request));
    }

    private RegraRecorrencia CriarRegraRecorrencia(AtualizarContaReceberRequest request, RecorrenciaConfigRequest recorrencia)
    {
        var tipoDia = MapearTipoDiaDominio(recorrencia.TipoDia);
        var dataInicio = ResolveDataInicioRecorrencia(request.DataEmissao, recorrencia);
        var dataFim = ResolveDataFimRecorrencia(recorrencia);

        return RegraRecorrencia.Criar(
            TipoLancamentoRecorrencia.ContaReceber,
            MapearTipoPeriodicidadeDominio(recorrencia.TipoPeriodicidade),
            tipoDia,
            recorrencia.DiaOrdemMensal,
            dataInicio,
            dataFim,
            recorrencia.PermiteEdicaoOcorrenciaIndividual,
            recorrencia.Observacao,
            SerializarTemplate(request));
    }

    private static DateOnly ResolveDataInicioRecorrencia(DateOnly dataEmissao, RecorrenciaConfigRequest recorrencia)
    {
        if (recorrencia.DataInicio.HasValue)
        {
            return RecorrenciaDateHelper.CalculateDateForReferenceMonth(
                recorrencia.DataInicio.Value,
                MapearTipoDiaDominio(recorrencia.TipoDia),
                recorrencia.DiaOrdemMensal);
        }

        return RecorrenciaDateHelper.CalculateAutomaticStartDate(
            dataEmissao,
            MapearTipoDiaDominio(recorrencia.TipoDia),
            recorrencia.DiaOrdemMensal);
    }

    private static DateOnly? ResolveDataFimRecorrencia(RecorrenciaConfigRequest recorrencia)
    {
        if (!recorrencia.DataFim.HasValue)
        {
            return null;
        }

        return RecorrenciaDateHelper.CalculateDateForReferenceMonth(
            recorrencia.DataFim.Value,
            MapearTipoDiaDominio(recorrencia.TipoDia),
            recorrencia.DiaOrdemMensal);
    }

    private static string SerializarTemplate(CriarContaReceberRequest request)
    {
        return JsonSerializer.Serialize(new ContaReceberRecorrenciaTemplate(
            request.NumeroDocumento,
            request.DataEmissao,
            request.ResponsavelId,
            request.PagadorId,
            request.DataVencimento,
            request.FormaPagamentoId,
            request.CartaoId,
            request.ContaBancariaId,
            request.ValorOriginal,
            request.ValorDesconto,
            request.ValorJuros,
            request.ValorMulta,
            request.Descricao,
            request.Observacao,
            request.Rateios.Select(x => new RateioRecorrenciaTemplate(x.ContaGerencialId, x.Valor)).ToArray()));
    }

    private static string SerializarTemplate(AtualizarContaReceberRequest request)
    {
        return JsonSerializer.Serialize(new ContaReceberRecorrenciaTemplate(
            request.NumeroDocumento,
            request.DataEmissao,
            request.ResponsavelId,
            request.PagadorId,
            request.DataVencimento,
            request.FormaPagamentoId,
            request.CartaoId,
            request.ContaBancariaId,
            request.ValorOriginal,
            request.ValorDesconto,
            request.ValorJuros,
            request.ValorMulta,
            request.Descricao,
            request.Observacao,
            request.Rateios.Select(x => new RateioRecorrenciaTemplate(x.ContaGerencialId, x.Valor)).ToArray()));
    }

    private static ContaReceberRecorrenciaTemplate DesserializarTemplate(string templateJson)
    {
        return JsonSerializer.Deserialize<ContaReceberRecorrenciaTemplate>(templateJson)
               ?? throw new InvalidOperationException("Template de recorrência inválido.");
    }

    private static AtualizarContaReceberRequest AjustarRequestParaMes(AtualizarContaReceberRequest request, int monthOffset)
    {
        return request with
        {
            DataEmissao = RecorrenciaDateHelper.Shift(request.DataEmissao, monthOffset),
            DataVencimento = RecorrenciaDateHelper.Shift(request.DataVencimento, monthOffset)
        };
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

    private static ContaReceber CriarOcorrenciaRecorrente(
        ContaReceberRecorrenciaTemplate template,
        Guid regraRecorrenciaId,
        DateOnly dataVencimento)
    {
        var monthOffset = RecorrenciaDateHelper.CalculateMonthOffset(template.DataVencimento, dataVencimento);

        return ContaReceber.Criar(
            template.NumeroDocumento,
            RecorrenciaDateHelper.Shift(template.DataEmissao, monthOffset),
            template.ResponsavelId,
            template.PagadorId,
            dataVencimento,
            template.FormaPagamentoId,
            template.CartaoId,
            template.ContaBancariaId,
            template.ValorOriginal,
            template.ValorDesconto,
            template.ValorJuros,
            template.ValorMulta,
            1,
            1,
            null,
            template.Descricao,
            template.Observacao,
            StatusConta.PendenteId,
            true,
            regraRecorrenciaId,
            OrigemLancamento.Recorrencia,
            template.Rateios.Select(x => RateioPlano.Create(x.ContaGerencialId, x.Valor)).ToArray());
    }

    private static void AtualizarContaExistente(ContaReceber conta, AtualizarContaReceberRequest request)
    {
        try
        {
            conta.Atualizar(
                request.NumeroDocumento,
                request.DataEmissao,
                request.ResponsavelId,
                request.PagadorId,
                request.DataVencimento,
                request.FormaPagamentoId,
                request.CartaoId,
                request.ContaBancariaId,
                request.ValorOriginal,
                request.ValorDesconto,
                request.ValorJuros,
                request.ValorMulta,
                request.Descricao,
                request.Observacao,
                StatusConta.PendenteId,
                ConverterRateios(request.Rateios));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            throw ConverterParaValidacao(exception);
        }
    }

    private async Task SincronizarRateiosContaAsync(ContaReceber conta, CancellationToken cancellationToken)
    {
        var rateiosExistentes = await dbContext.RateiosContaGerencial
            .Where(x => x.ContaReceberId == conta.Id)
            .ToListAsync(cancellationToken);

        dbContext.RateiosContaGerencial.RemoveRange(rateiosExistentes);
        dbContext.RateiosContaGerencial.AddRange(conta.Rateios);
    }

    private async Task<RegraRecorrencia> ObterRegraRecorrenciaObrigatoriaAsync(
        ContaReceber conta,
        CancellationToken cancellationToken)
    {
        if (!conta.RegraRecorrenciaId.HasValue)
        {
            throw ValidationExceptionFactory.Create("Recorrencia", "A conta informada não possui regra de recorrência.");
        }

        return await dbContext.RegrasRecorrencia
            .SingleAsync(x => x.Id == conta.RegraRecorrenciaId.Value, cancellationToken);
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

    private sealed record ContaReceberRecorrenciaTemplate(
        string? NumeroDocumento,
        DateOnly DataEmissao,
        Guid? ResponsavelId,
        Guid PagadorId,
        DateOnly DataVencimento,
        Guid FormaPagamentoId,
        Guid? CartaoId,
        Guid? ContaBancariaId,
        decimal ValorOriginal,
        decimal ValorDesconto,
        decimal ValorJuros,
        decimal ValorMulta,
        string Descricao,
        string? Observacao,
        IReadOnlyCollection<RateioRecorrenciaTemplate> Rateios);

    private sealed record RateioRecorrenciaTemplate(Guid ContaGerencialId, decimal Valor);
}


