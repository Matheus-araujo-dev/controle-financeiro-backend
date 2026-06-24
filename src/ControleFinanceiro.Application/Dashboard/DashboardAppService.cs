using System.Globalization;
using System.Text.Json;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Application.ImportacoesWhatsapp;
using ControleFinanceiro.Contracts.Dashboard;
using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;
using ControleFinanceiro.Domain.Financeiro;
using ControleFinanceiro.Domain.ImportacoesWhatsapp;
using ControleFinanceiro.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ControleFinanceiro.Application.Dashboard;

public sealed class DashboardAppService
{
    private const int MaxObservacaoLength = 50;

    private static readonly IReadOnlyDictionary<Guid, StatusContaInfo> StatusContasLookup =
        StatusConta.Seeds().ToDictionary(s => s.Id, s => new StatusContaInfo(s.Codigo, s.Nome));

    private readonly IAppDbContext dbContext;
    private readonly ICurrentUser currentUser;
    private readonly ILogger<DashboardAppService> logger;

    public DashboardAppService(
        IAppDbContext dbContext,
        ICurrentUser currentUser,
        ILogger<DashboardAppService> logger)
    {
        this.dbContext = dbContext;
        this.currentUser = currentUser;
        this.logger = logger;
    }

    public async Task<DashboardResumoResponse> ObterResumoAsync(
        DashboardResumoQueryRequest query,
        CancellationToken cancellationToken)
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        DateOnly dataReferencia;
        DateOnly dataFinal;

        if (!string.IsNullOrWhiteSpace(query.MesReferencia))
        {
            var inicioMes = ParseMesReferencia(query.MesReferencia);
            var fimMes = inicioMes.AddMonths(1).AddDays(-1);
            dataReferencia = hoje >= inicioMes && hoje <= fimMes ? hoje : inicioMes;
            dataFinal = fimMes;
        }
        else
        {
            dataReferencia = query.DataReferencia ?? hoje;
            dataFinal = dataReferencia.AddDays(query.DiasProjetados);
        }

        var pessoas = await CarregarPessoasAsync(cancellationToken);

        var saldoAtual = await CalcularSaldoRealizadoAteAsync(hoje, cancellationToken);
        var totalAPagar = await CalcularTotalPendenteContasPagarAsync(dataFinal, cancellationToken);
        var totalAReceber = await CalcularTotalPendenteContasReceberAsync(dataFinal, cancellationToken);

        var contasVencidas = await CarregarContasVencidasAsync(dataReferencia, pessoas, cancellationToken);
        var contasAVencer = await CarregarContasAVencerAsync(dataReferencia, dataFinal, pessoas, cancellationToken);
        var movimentacoesRecentes = await CarregarMovimentacoesRecentesAsync(dataReferencia, cancellationToken);

        var saldoProjetado = decimal.Round(saldoAtual + totalAReceber - totalAPagar, 2, MidpointRounding.AwayFromZero);
        var riscoSaldoNegativo = saldoProjetado < 0;

        return new DashboardResumoResponse(
            saldoAtual,
            totalAPagar,
            totalAReceber,
            saldoProjetado,
            riscoSaldoNegativo,
            contasVencidas,
            contasAVencer,
            movimentacoesRecentes);
    }

    public async Task<DashboardFluxoCaixaResponse> ObterFluxoCaixaAsync(
        DashboardFluxoCaixaQueryRequest query,
        CancellationToken cancellationToken)
    {
        var (dataInicial, dias, mesReferenciaEhMesAtual) = ResolverJanela(query.MesReferencia, query.DataInicial, query.Dias);
        var dataFinal = dataInicial.AddDays(dias - 1);
        var usarDataVencimento = query.Visao == DashboardFluxoCaixaVisao.Caixa;
        var projetarPrevisoes = !mesReferenciaEhMesAtual;

        var saldoInicial = await CalcularSaldoRealizadoAteAsync(dataInicial.AddDays(-1), cancellationToken);

        var eventos = new List<FluxoCaixaEvento>();

        var contasPagar = await dbContext.ContasPagar
            .AsNoTracking()
            .Where(c =>
                c.StatusContaId != StatusConta.LiquidadaId &&
                c.StatusContaId != StatusConta.CanceladaId &&
                (usarDataVencimento
                    ? c.DataVencimento >= dataInicial && c.DataVencimento <= dataFinal
                    : c.DataEmissao >= dataInicial && c.DataEmissao <= dataFinal))
            .Select(c => new { c.DataEmissao, c.DataVencimento, c.ValorLiquido })
            .ToListAsync(cancellationToken);

        foreach (var conta in contasPagar)
        {
            var data = usarDataVencimento ? conta.DataVencimento : conta.DataEmissao;
            if (data >= dataInicial && data <= dataFinal)
            {
                eventos.Add(new FluxoCaixaEvento(data, TipoMovimentacao.Saida, conta.ValorLiquido));
            }
        }

        var contasReceber = await dbContext.ContasReceber
            .AsNoTracking()
            .Where(c =>
                c.StatusContaId != StatusConta.LiquidadaId &&
                c.StatusContaId != StatusConta.CanceladaId &&
                (usarDataVencimento
                    ? c.DataVencimento >= dataInicial && c.DataVencimento <= dataFinal
                    : c.DataEmissao >= dataInicial && c.DataEmissao <= dataFinal))
            .Select(c => new { c.DataEmissao, c.DataVencimento, c.ValorLiquido })
            .ToListAsync(cancellationToken);

        foreach (var conta in contasReceber)
        {
            var data = usarDataVencimento ? conta.DataVencimento : conta.DataEmissao;
            if (data >= dataInicial && data <= dataFinal)
            {
                eventos.Add(new FluxoCaixaEvento(data, TipoMovimentacao.Entrada, conta.ValorLiquido));
            }
        }

        var movimentacoes = await dbContext.MovimentacoesFinanceiras
            .AsNoTracking()
            .Where(m =>
                m.Natureza == NaturezaMovimentacao.Realizada &&
                m.StatusMovimentacaoId != StatusMovimentacao.CanceladaId &&
                m.DataMovimentacao >= dataInicial &&
                m.DataMovimentacao <= dataFinal)
            .Select(m => new { m.DataMovimentacao, m.Tipo, m.Valor })
            .ToListAsync(cancellationToken);

        eventos.AddRange(movimentacoes.Select(m => new FluxoCaixaEvento(m.DataMovimentacao, m.Tipo, m.Valor)));

        var comprasImportadas = await CarregarComprasImportadasAsync(cancellationToken);

        foreach (var compra in comprasImportadas)
        {
            var data = usarDataVencimento ? compra.DataVencimento : compra.DataCompra;
            if (data >= dataInicial && data <= dataFinal)
            {
                eventos.Add(new FluxoCaixaEvento(data, compra.Tipo, compra.Valor));
            }
        }

        if (projetarPrevisoes)
        {
            var recorrencias = await ProjetarRecorrenciasAsync(dataInicial, dataFinal, cancellationToken);
            eventos.AddRange(recorrencias.Select(r => new FluxoCaixaEvento(r.Data, r.Tipo, r.Valor)));

            eventos.AddRange(ProjetarComprasImportadas(comprasImportadas, dataInicial, dataFinal, usarDataVencimento)
                .Select(p => new FluxoCaixaEvento(p.Data, p.Compra.Tipo, p.Compra.Valor)));
        }

        var fluxo = FluxoCaixaDiario.Projetar(dataInicial, dias, saldoInicial, eventos);

        return new DashboardFluxoCaixaResponse(
            query.Visao,
            dataInicial,
            dias,
            fluxo.Any(dia => dia.RiscoSaldoNegativo),
            fluxo.Select(dia => new DashboardFluxoCaixaDiaResponse(
                dia.Data,
                dia.SaldoInicial,
                dia.EntradasPrevistas,
                dia.SaidasPrevistas,
                dia.SaldoFinalPrevisto,
                dia.RiscoSaldoNegativo)).ToList());
    }

    public async Task<DashboardContaGerencialResumoResponse> ObterContasGerenciaisResumoAsync(
        DashboardContaGerencialResumoQueryRequest query,
        CancellationToken cancellationToken)
    {
        var tipoFiltro = ParseTipoContaGerencial(query.Tipo);
        var (dataInicial, dias, _) = ResolverJanela(query.MesReferencia, query.DataInicial, query.Dias);
        var dataFinal = dataInicial.AddDays(dias - 1);

        var rateios = await CarregarRateiosPorEmissaoAsync(dataInicial, dataFinal, cancellationToken);
        var contasGerenciais = await CarregarContasGerenciaisAsync(cancellationToken);

        var itens = rateios
            .GroupBy(r => r.ContaGerencialId)
            .Where(g => contasGerenciais.ContainsKey(g.Key))
            .Select(g =>
            {
                var contaGerencial = contasGerenciais[g.Key];
                return new DashboardContaGerencialResumoItemResponse(
                    g.Key,
                    contaGerencial.Codigo,
                    contaGerencial.Descricao,
                    contaGerencial.Tipo.ToString(),
                    decimal.Round(g.Sum(r => r.ValorRateio), 2, MidpointRounding.AwayFromZero),
                    g.Select(r => r.LancamentoId).Distinct().Count(),
                    g.Max(r => r.DataEmissao));
            })
            .Where(item => !tipoFiltro.HasValue || item.Tipo == tipoFiltro.Value.ToString())
            .OrderByDescending(item => item.ValorTotal)
            .ThenBy(item => item.Descricao)
            .ToList();

        var totalReceitas = decimal.Round(
            itens.Where(item => item.Tipo == nameof(TipoContaGerencial.Receita)).Sum(item => item.ValorTotal),
            2,
            MidpointRounding.AwayFromZero);
        var totalDespesas = decimal.Round(
            itens.Where(item => item.Tipo == nameof(TipoContaGerencial.Despesa)).Sum(item => item.ValorTotal),
            2,
            MidpointRounding.AwayFromZero);

        return new DashboardContaGerencialResumoResponse(
            dataInicial,
            dias,
            totalReceitas,
            totalDespesas,
            decimal.Round(totalReceitas - totalDespesas, 2, MidpointRounding.AwayFromZero),
            itens);
    }

    public async Task<DashboardContaGerencialSerieResponse> ObterContasGerenciaisSerieAsync(
        DashboardContaGerencialSerieQueryRequest query,
        CancellationToken cancellationToken)
    {
        var tipoFiltro = ParseTipoContaGerencial(query.Tipo);
        var (dataInicial, dias, _) = ResolverJanela(query.MesReferencia, query.DataInicial, query.Dias);
        var dataFinal = dataInicial.AddDays(dias - 1);

        var rateios = await CarregarRateiosPorEmissaoAsync(dataInicial, dataFinal, cancellationToken);
        var contasGerenciais = await CarregarContasGerenciaisAsync(cancellationToken);

        var rateiosFiltrados = rateios
            .Where(r => contasGerenciais.TryGetValue(r.ContaGerencialId, out var contaGerencial) &&
                        (!tipoFiltro.HasValue || contaGerencial.Tipo == tipoFiltro.Value) &&
                        (!query.ContaGerencialId.HasValue || r.ContaGerencialId == query.ContaGerencialId.Value))
            .ToList();

        var itens = new List<DashboardContaGerencialSerieDiaResponse>(dias);

        for (var indice = 0; indice < dias; indice++)
        {
            var data = dataInicial.AddDays(indice);
            var rateiosDia = rateiosFiltrados.Where(r => r.DataEmissao == data).ToList();
            var totalReceitas = decimal.Round(
                rateiosDia.Where(r => contasGerenciais[r.ContaGerencialId].Tipo == TipoContaGerencial.Receita).Sum(r => r.ValorRateio),
                2,
                MidpointRounding.AwayFromZero);
            var totalDespesas = decimal.Round(
                rateiosDia.Where(r => contasGerenciais[r.ContaGerencialId].Tipo == TipoContaGerencial.Despesa).Sum(r => r.ValorRateio),
                2,
                MidpointRounding.AwayFromZero);

            itens.Add(new DashboardContaGerencialSerieDiaResponse(
                data,
                totalReceitas,
                totalDespesas,
                decimal.Round(totalReceitas - totalDespesas, 2, MidpointRounding.AwayFromZero)));
        }

        return new DashboardContaGerencialSerieResponse(
            dataInicial,
            dias,
            query.Tipo,
            query.ContaGerencialId,
            itens);
    }

    public async Task<DashboardContaGerencialLancamentosResponse> ObterContaGerencialLancamentosAsync(
        DashboardContaGerencialLancamentosQueryRequest query,
        CancellationToken cancellationToken)
    {
        ParseTipoContaGerencial(query.Tipo);

        if (!query.ContaGerencialId.HasValue || query.ContaGerencialId.Value == Guid.Empty)
        {
            throw ValidationExceptionFactory.Create("ContaGerencialId", "Conta gerencial é obrigatória.");
        }

        var (dataInicial, dias, _) = ResolverJanela(query.MesReferencia, query.DataInicial, query.Dias);
        var dataFinal = dataInicial.AddDays(dias - 1);

        var contaGerencial = await dbContext.ContasGerenciais
            .AsNoTracking()
            .Where(c => c.Id == query.ContaGerencialId.Value)
            .Select(c => new ContaGerencialInfo(c.Codigo, c.Descricao, c.Tipo))
            .SingleOrDefaultAsync(cancellationToken);

        if (contaGerencial is null)
        {
            throw ValidationExceptionFactory.Create("ContaGerencialId", "Conta gerencial não encontrada.");
        }

        var pessoas = await CarregarPessoasAsync(cancellationToken);
        var rateios = await CarregarRateiosPorEmissaoAsync(dataInicial, dataFinal, cancellationToken);

        var itens = rateios
            .Where(r => r.ContaGerencialId == query.ContaGerencialId.Value)
            .OrderBy(r => r.DataEmissao)
            .ThenBy(r => r.Descricao)
            .Select(r =>
            {
                var status = StatusContasLookup.GetValueOrDefault(r.StatusContaId, new StatusContaInfo(string.Empty, string.Empty));
                return new DashboardContaGerencialLancamentoItemResponse(
                    r.LancamentoId,
                    r.TipoLancamento,
                    r.Descricao,
                    pessoas.GetValueOrDefault(r.PessoaId, string.Empty),
                    r.DataEmissao,
                    r.DataVencimento,
                    r.ValorLancamento,
                    r.ValorRateio,
                    status.Codigo,
                    status.Nome);
            })
            .ToList();

        return new DashboardContaGerencialLancamentosResponse(
            dataInicial,
            dias,
            query.Tipo!,
            query.ContaGerencialId.Value,
            contaGerencial.Codigo,
            contaGerencial.Descricao,
            itens);
    }

    public async Task<DashboardCentralPrevisaoResumoResponse> ObterCentralPrevisaoResumoAsync(
        DashboardCentralPrevisaoQueryRequest query,
        CancellationToken cancellationToken)
    {
        var (dataInicial, dias, mesReferenciaEhMesAtual) = ResolverJanela(query.MesReferencia, query.DataInicial, query.Dias);

        var itens = await ConstruirItensPrevisaoAsync(dataInicial, dias, !mesReferenciaEhMesAtual, cancellationToken);

        var grupos = itens
            .Where(item =>
                (!query.Origem.HasValue || item.Origem == query.Origem.Value) &&
                (!query.Status.HasValue || item.Status == query.Status.Value))
            .GroupBy(item => new { item.Data, item.Tipo, item.Origem, item.Status })
            .OrderBy(g => g.Key.Data)
            .ThenBy(g => g.Key.Origem)
            .ThenBy(g => g.Key.Status)
            .Select(g => new DashboardCentralPrevisaoResumoItemResponse(
                g.Key.Data,
                MapearTipoMovimentacao(g.Key.Tipo),
                g.Key.Origem,
                g.Key.Status,
                g.Count(),
                decimal.Round(g.Sum(item => item.Valor), 2, MidpointRounding.AwayFromZero)))
            .ToList();

        return new DashboardCentralPrevisaoResumoResponse(
            dataInicial,
            dias,
            query.Origem,
            query.Status,
            grupos);
    }

    public async Task<DashboardCentralPrevisaoItensResponse> ObterCentralPrevisaoItensAsync(
        DashboardCentralPrevisaoItensQueryRequest query,
        CancellationToken cancellationToken)
    {
        var (dataInicial, dias, mesReferenciaEhMesAtual) = ResolverJanela(query.MesReferencia, query.DataInicial, query.Dias);

        var itensPrevisao = await ConstruirItensPrevisaoAsync(dataInicial, dias, !mesReferenciaEhMesAtual, cancellationToken);
        var contasGerenciais = await CarregarContasGerenciaisAsync(cancellationToken);

        var itens = itensPrevisao
            .Where(item =>
                (!query.Data.HasValue || item.Data == query.Data.Value) &&
                (!query.Origem.HasValue || item.Origem == query.Origem.Value) &&
                (!query.Status.HasValue || item.Status == query.Status.Value))
            .OrderBy(item => item.Data)
            .ThenBy(item => item.Descricao)
            .Select(item =>
            {
                var contaGerencial = item.ContaGerencialId.HasValue
                    ? contasGerenciais.GetValueOrDefault(item.ContaGerencialId.Value)
                    : null;

                return new DashboardCentralPrevisaoItemResponse(
                    item.TipoReferencia,
                    item.ReferenciaId,
                    item.Data,
                    MapearTipoMovimentacao(item.Tipo),
                    item.Origem,
                    item.Status,
                    item.Descricao,
                    item.Valor,
                    item.PessoaNome,
                    item.ResponsavelNome,
                    item.ContaGerencialId,
                    contaGerencial?.Codigo,
                    contaGerencial?.Descricao);
            })
            .ToList();

        return new DashboardCentralPrevisaoItensResponse(
            dataInicial,
            dias,
            query.Data,
            query.Origem,
            query.Status,
            itens);
    }

    private async Task<List<PrevisaoItem>> ConstruirItensPrevisaoAsync(
        DateOnly dataInicial,
        int dias,
        bool incluirRecorrencias,
        CancellationToken cancellationToken)
    {
        var dataFinal = dataInicial.AddDays(dias - 1);
        var pessoas = await CarregarPessoasAsync(cancellationToken);
        var itens = new List<PrevisaoItem>();

        var contasPagar = await dbContext.ContasPagar
            .AsNoTracking()
            .Where(c =>
                c.StatusContaId != StatusConta.CanceladaId &&
                c.DataVencimento >= dataInicial &&
                c.DataVencimento <= dataFinal)
            .Select(c => new ContaJanelaInfo(
                c.Id,
                "ContaPagar",
                c.Descricao,
                c.DataVencimento,
                c.ValorLiquido,
                c.StatusContaId,
                c.RecebedorId,
                c.ResponsavelCompraId,
                c.RegraRecorrenciaId,
                c.Origem))
            .ToListAsync(cancellationToken);

        var contasReceber = await dbContext.ContasReceber
            .AsNoTracking()
            .Where(c =>
                c.StatusContaId != StatusConta.CanceladaId &&
                c.DataVencimento >= dataInicial &&
                c.DataVencimento <= dataFinal)
            .Select(c => new ContaJanelaInfo(
                c.Id,
                "ContaReceber",
                c.Descricao,
                c.DataVencimento,
                c.ValorLiquido,
                c.StatusContaId,
                c.PagadorId,
                c.ResponsavelId,
                c.RegraRecorrenciaId,
                c.Origem))
            .ToListAsync(cancellationToken);

        var recorrencias = incluirRecorrencias
            ? await ProjetarRecorrenciasAsync(dataInicial, dataFinal, cancellationToken)
            : [];

        var rateioPrincipal = await CarregarRateioPrincipalAsync(
            contasPagar.Where(c => c.TipoLancamento == "ContaPagar").Select(c => c.Id)
                .Concat(recorrencias.Where(r => r.EhContaPagar).Select(r => r.ContaTemplateId))
                .Distinct()
                .ToArray(),
            contasReceber.Select(c => c.Id)
                .Concat(recorrencias.Where(r => !r.EhContaPagar).Select(r => r.ContaTemplateId))
                .Distinct()
                .ToArray(),
            cancellationToken);

        foreach (var conta in contasPagar.Concat(contasReceber))
        {
            var origem = ClassificarOrigemConta(conta.Origem, conta.RegraRecorrenciaId);

            if (!incluirRecorrencias &&
                origem is DashboardCentralPrevisaoOrigem.Recorrencia or DashboardCentralPrevisaoOrigem.ContaFuturaGerada)
            {
                continue;
            }

            var ehContaPagar = conta.TipoLancamento == "ContaPagar";
            var lookup = ehContaPagar ? rateioPrincipal.PorContaPagar : rateioPrincipal.PorContaReceber;

            itens.Add(new PrevisaoItem(
                conta.TipoLancamento,
                conta.Id,
                conta.DataVencimento,
                ehContaPagar ? TipoMovimentacao.Saida : TipoMovimentacao.Entrada,
                origem,
                conta.StatusContaId == StatusConta.LiquidadaId
                    ? DashboardCentralPrevisaoStatus.Realizado
                    : DashboardCentralPrevisaoStatus.Substituido,
                conta.Descricao,
                conta.ValorLiquido,
                pessoas.GetValueOrDefault(conta.PessoaId),
                conta.ResponsavelId.HasValue ? pessoas.GetValueOrDefault(conta.ResponsavelId.Value) : null,
                lookup.TryGetValue(conta.Id, out var contaGerencialId) ? contaGerencialId : null));
        }

        foreach (var recorrencia in recorrencias)
        {
            var lookup = recorrencia.EhContaPagar ? rateioPrincipal.PorContaPagar : rateioPrincipal.PorContaReceber;

            itens.Add(new PrevisaoItem(
                "RegraRecorrencia",
                recorrencia.RegraId,
                recorrencia.Data,
                recorrencia.Tipo,
                DashboardCentralPrevisaoOrigem.Recorrencia,
                DashboardCentralPrevisaoStatus.Previsto,
                recorrencia.Descricao,
                recorrencia.Valor,
                pessoas.GetValueOrDefault(recorrencia.PessoaId),
                recorrencia.ResponsavelId.HasValue ? pessoas.GetValueOrDefault(recorrencia.ResponsavelId.Value) : null,
                lookup.TryGetValue(recorrencia.ContaTemplateId, out var contaGerencialId) ? contaGerencialId : null));
        }

        var comprasImportadas = await CarregarComprasImportadasAsync(cancellationToken);

        foreach (var compra in comprasImportadas)
        {
            if (compra.DataVencimento < dataInicial || compra.DataVencimento > dataFinal)
            {
                continue;
            }

            itens.Add(CriarItemCompraImportada(compra, compra.DataVencimento, DashboardCentralPrevisaoStatus.Substituido, pessoas));
        }

        if (incluirRecorrencias)
        {
            foreach (var projecao in ProjetarComprasImportadas(comprasImportadas, dataInicial, dataFinal, usarDataVencimento: true))
            {
                itens.Add(CriarItemCompraImportada(projecao.Compra, projecao.Data, DashboardCentralPrevisaoStatus.Previsto, pessoas));
            }
        }

        return itens;
    }

    private static PrevisaoItem CriarItemCompraImportada(
        ImportacaoCompraInfo compra,
        DateOnly data,
        DashboardCentralPrevisaoStatus status,
        IReadOnlyDictionary<Guid, string> pessoas)
    {
        return new PrevisaoItem(
            "ItemImportadoWhatsapp",
            compra.Id,
            data,
            compra.Tipo,
            compra.Recorrente
                ? DashboardCentralPrevisaoOrigem.CompraRecorrenteImportada
                : DashboardCentralPrevisaoOrigem.Parcela,
            status,
            compra.Descricao,
            compra.Valor,
            null,
            compra.ResponsavelId.HasValue ? pessoas.GetValueOrDefault(compra.ResponsavelId.Value) : null,
            compra.ContaGerencialId);
    }

    private static DashboardCentralPrevisaoOrigem ClassificarOrigemConta(OrigemLancamento origem, Guid? regraRecorrenciaId)
    {
        if (origem == OrigemLancamento.Recorrencia)
        {
            return DashboardCentralPrevisaoOrigem.ContaFuturaGerada;
        }

        return regraRecorrenciaId.HasValue
            ? DashboardCentralPrevisaoOrigem.Recorrencia
            : DashboardCentralPrevisaoOrigem.Parcela;
    }

    private async Task<List<RecorrenciaProjetada>> ProjetarRecorrenciasAsync(
        DateOnly dataInicial,
        DateOnly dataFinal,
        CancellationToken cancellationToken)
    {
        var regras = await dbContext.RegrasRecorrencia
            .AsNoTracking()
            .Where(r => r.Ativa)
            .ToListAsync(cancellationToken);

        if (regras.Count == 0)
        {
            return [];
        }

        var contasPagarRegra = await dbContext.ContasPagar
            .AsNoTracking()
            .Where(c => c.RegraRecorrenciaId != null && c.StatusContaId != StatusConta.CanceladaId)
            .Select(c => new ContaRecorrenciaInfo(
                c.Id,
                c.RegraRecorrenciaId!.Value,
                c.DataVencimento,
                c.ValorLiquido,
                c.Descricao,
                c.Origem,
                c.RecebedorId,
                c.ResponsavelCompraId))
            .ToListAsync(cancellationToken);

        var contasReceberRegra = await dbContext.ContasReceber
            .AsNoTracking()
            .Where(c => c.RegraRecorrenciaId != null && c.StatusContaId != StatusConta.CanceladaId)
            .Select(c => new ContaRecorrenciaInfo(
                c.Id,
                c.RegraRecorrenciaId!.Value,
                c.DataVencimento,
                c.ValorLiquido,
                c.Descricao,
                c.Origem,
                c.PagadorId,
                c.ResponsavelId))
            .ToListAsync(cancellationToken);

        var projecoes = new List<RecorrenciaProjetada>();

        foreach (var regra in regras)
        {
            var ehContaPagar = regra.TipoLancamento == TipoLancamentoRecorrencia.ContaPagar;
            var contasDaRegra = (ehContaPagar ? contasPagarRegra : contasReceberRegra)
                .Where(c => c.RegraId == regra.Id)
                .OrderBy(c => c.DataVencimento)
                .ToList();

            if (contasDaRegra.Count == 0)
            {
                continue;
            }

            var template = contasDaRegra.FirstOrDefault(c => c.Origem != OrigemLancamento.Recorrencia) ?? contasDaRegra[^1];
            var mesesComOcorrencia = contasDaRegra
                .Select(c => new DateOnly(c.DataVencimento.Year, c.DataVencimento.Month, 1))
                .ToHashSet();

            foreach (var data in CalcularDatasProjetadas(regra, mesesComOcorrencia, dataInicial, dataFinal))
            {
                projecoes.Add(new RecorrenciaProjetada(
                    regra.Id,
                    data,
                    ehContaPagar ? TipoMovimentacao.Saida : TipoMovimentacao.Entrada,
                    template.ValorLiquido,
                    template.Descricao,
                    template.PessoaId,
                    template.ResponsavelId,
                    template.Id,
                    ehContaPagar));
            }
        }

        return projecoes;
    }

    private static IEnumerable<DateOnly> CalcularDatasProjetadas(
        RegraRecorrencia regra,
        HashSet<DateOnly> mesesComOcorrencia,
        DateOnly dataInicial,
        DateOnly dataFinal)
    {
        var mes = new DateOnly(dataInicial.Year, dataInicial.Month, 1);
        var mesFinal = new DateOnly(dataFinal.Year, dataFinal.Month, 1);

        while (mes <= mesFinal)
        {
            if (!mesesComOcorrencia.Contains(mes))
            {
                var data = regra.CalcularDataParaMes(mes.Year, mes.Month);

                if (data >= dataInicial &&
                    data <= dataFinal &&
                    data >= regra.DataInicio &&
                    (!regra.DataFim.HasValue || data <= regra.DataFim.Value))
                {
                    yield return data;
                }
            }

            mes = mes.AddMonths(1);
        }
    }

    private async Task<List<ImportacaoCompraInfo>> CarregarComprasImportadasAsync(CancellationToken cancellationToken)
    {
        // Itens importados via webhook anônimo podem existir sem família atribuída
        // (FamiliaId vazio); o filtro global de tenant os esconderia. Eles entram na
        // leitura junto com os itens da família corrente.
        var familiaId = currentUser.FamiliaId;

        var registros = await dbContext.ItensImportadosWhatsapp
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(i =>
                i.TipoSugestao == TipoSugestaoImportacaoWhatsapp.CompraCartao &&
                i.Status == StatusItemImportadoWhatsapp.Confirmado &&
                (familiaId == null || i.FamiliaId == familiaId.Value || i.FamiliaId == Guid.Empty))
            .ToListAsync(cancellationToken);

        var compras = new List<ImportacaoCompraInfo>(registros.Count);

        foreach (var item in registros)
        {
            ImportacaoWhatsappSuggestionPayload payload;

            try
            {
                payload = ImportacaoWhatsappSuggestionPayload.Parse(item.PayloadSugeridoJson);
            }
            catch (JsonException exception)
            {
                logger.LogWarning(exception, "Payload inválido no item importado {ItemId}; item ignorado no dashboard.", item.Id);
                continue;
            }

            var dataVencimento = payload.DataVencimento ?? payload.DataIdentificada;
            var dataCompra = payload.DataIdentificada ?? payload.DataVencimento;

            if (!dataVencimento.HasValue || !dataCompra.HasValue || !payload.Valor.HasValue)
            {
                continue;
            }

            var tipo = string.Equals(payload.TipoMovimentacaoSugerido, "Entrada", StringComparison.OrdinalIgnoreCase)
                ? TipoMovimentacao.Entrada
                : TipoMovimentacao.Saida;

            compras.Add(new ImportacaoCompraInfo(
                item.Id,
                item.DescricaoAjustada ?? payload.Descricao ?? "Compra importada",
                payload.Valor.Value,
                dataCompra.Value,
                dataVencimento.Value,
                tipo,
                item.MarcarComoRecorrente,
                payload.GetParcelamentoCompraCartaoInfo(),
                payload.BuildRecurringSeriesKey(),
                payload.BuildInstallmentSeriesKey(),
                item.ContaGerencialId,
                item.ResponsavelId));
        }

        return compras;
    }

    private static IEnumerable<(ImportacaoCompraInfo Compra, DateOnly Data)> ProjetarComprasImportadas(
        IReadOnlyCollection<ImportacaoCompraInfo> compras,
        DateOnly dataInicial,
        DateOnly dataFinal,
        bool usarDataVencimento)
    {
        DateOnly DataDe(ImportacaoCompraInfo compra) => usarDataVencimento ? compra.DataVencimento : compra.DataCompra;

        foreach (var grupo in compras.Where(c => c.Recorrente).GroupBy(c => c.SerieRecorrenteKey ?? c.Id.ToString()))
        {
            var semente = grupo.OrderBy(DataDe).Last();
            var mesesExistentes = grupo
                .Select(c => new DateOnly(DataDe(c).Year, DataDe(c).Month, 1))
                .ToHashSet();

            for (var deslocamento = 1; ; deslocamento++)
            {
                var data = DataDe(semente).AddMonths(deslocamento);

                if (data > dataFinal)
                {
                    break;
                }

                if (data < dataInicial || mesesExistentes.Contains(new DateOnly(data.Year, data.Month, 1)))
                {
                    continue;
                }

                yield return (semente, data);
            }
        }

        foreach (var grupo in compras
                     .Where(c => !c.Recorrente && c.Parcelamento is not null)
                     .GroupBy(c => c.SerieParcelamentoKey ?? c.Id.ToString()))
        {
            var semente = grupo.OrderBy(c => c.Parcelamento!.NumeroParcela).Last();
            var mesesExistentes = grupo
                .Select(c => new DateOnly(DataDe(c).Year, DataDe(c).Month, 1))
                .ToHashSet();

            for (var parcela = semente.Parcelamento!.NumeroParcela + 1; parcela <= semente.Parcelamento.QuantidadeParcelas; parcela++)
            {
                var data = DataDe(semente).AddMonths(parcela - semente.Parcelamento.NumeroParcela);

                if (data > dataFinal)
                {
                    break;
                }

                if (data < dataInicial || mesesExistentes.Contains(new DateOnly(data.Year, data.Month, 1)))
                {
                    continue;
                }

                yield return (semente, data);
            }
        }
    }

    internal async Task<List<RateioLancamentoInfo>> CarregarRateiosPorEmissaoAsync(
        DateOnly dataInicial,
        DateOnly dataFinal,
        CancellationToken cancellationToken)
    {
        var rateiosPagar = await (
            from rateio in dbContext.RateiosContaGerencial.AsNoTracking()
            join conta in dbContext.ContasPagar.AsNoTracking() on rateio.ContaPagarId equals (Guid?)conta.Id
            where conta.DataEmissao >= dataInicial &&
                  conta.DataEmissao <= dataFinal &&
                  conta.StatusContaId != StatusConta.CanceladaId
            select new RateioLancamentoInfo(
                conta.Id,
                "ContaPagar",
                conta.Descricao,
                conta.RecebedorId,
                conta.DataEmissao,
                conta.DataVencimento,
                conta.ValorLiquido,
                rateio.Valor,
                conta.StatusContaId,
                rateio.ContaGerencialId))
            .ToListAsync(cancellationToken);

        var rateiosReceber = await (
            from rateio in dbContext.RateiosContaGerencial.AsNoTracking()
            join conta in dbContext.ContasReceber.AsNoTracking() on rateio.ContaReceberId equals (Guid?)conta.Id
            where conta.DataEmissao >= dataInicial &&
                  conta.DataEmissao <= dataFinal &&
                  conta.StatusContaId != StatusConta.CanceladaId
            select new RateioLancamentoInfo(
                conta.Id,
                "ContaReceber",
                conta.Descricao,
                conta.PagadorId,
                conta.DataEmissao,
                conta.DataVencimento,
                conta.ValorLiquido,
                rateio.Valor,
                conta.StatusContaId,
                rateio.ContaGerencialId))
            .ToListAsync(cancellationToken);

        rateiosPagar.AddRange(rateiosReceber);
        return rateiosPagar;
    }

    private async Task<(Dictionary<Guid, Guid?> PorContaPagar, Dictionary<Guid, Guid?> PorContaReceber)> CarregarRateioPrincipalAsync(
        IReadOnlyCollection<Guid> contasPagarIds,
        IReadOnlyCollection<Guid> contasReceberIds,
        CancellationToken cancellationToken)
    {
        if (contasPagarIds.Count == 0 && contasReceberIds.Count == 0)
        {
            return ([], []);
        }

        var rateios = await dbContext.RateiosContaGerencial
            .AsNoTracking()
            .Where(r =>
                (r.ContaPagarId != null && contasPagarIds.Contains(r.ContaPagarId.Value)) ||
                (r.ContaReceberId != null && contasReceberIds.Contains(r.ContaReceberId.Value)))
            .Select(r => new { r.ContaPagarId, r.ContaReceberId, r.ContaGerencialId, r.Valor })
            .ToListAsync(cancellationToken);

        var porContaPagar = rateios
            .Where(r => r.ContaPagarId.HasValue)
            .GroupBy(r => r.ContaPagarId!.Value)
            .ToDictionary(g => g.Key, g => (Guid?)g.OrderByDescending(r => r.Valor).First().ContaGerencialId);

        var porContaReceber = rateios
            .Where(r => r.ContaReceberId.HasValue)
            .GroupBy(r => r.ContaReceberId!.Value)
            .ToDictionary(g => g.Key, g => (Guid?)g.OrderByDescending(r => r.Valor).First().ContaGerencialId);

        return (porContaPagar, porContaReceber);
    }

    private async Task<decimal> CalcularSaldoRealizadoAteAsync(DateOnly dataLimite, CancellationToken cancellationToken)
    {
        var saldoInicialContas = await dbContext.ContasBancarias
            .AsNoTracking()
            .Where(c => c.Ativo)
            .SumAsync(c => (decimal?)c.SaldoInicial, cancellationToken) ?? 0m;

        var movimentos = await dbContext.MovimentacoesFinanceiras
            .AsNoTracking()
            .Where(m =>
                m.Natureza == NaturezaMovimentacao.Realizada &&
                m.StatusMovimentacaoId != StatusMovimentacao.CanceladaId &&
                m.DataMovimentacao <= dataLimite)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalEntradas = g.Sum(m => m.Tipo == TipoMovimentacao.Entrada ? m.Valor : 0),
                TotalSaidas = g.Sum(m => m.Tipo == TipoMovimentacao.Saida ? m.Valor : 0)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return decimal.Round(
            saldoInicialContas + (movimentos?.TotalEntradas ?? 0) - (movimentos?.TotalSaidas ?? 0),
            2,
            MidpointRounding.AwayFromZero);
    }

    private async Task<decimal> CalcularTotalPendenteContasPagarAsync(DateOnly dataFinal, CancellationToken cancellationToken)
    {
        return await dbContext.ContasPagar
            .AsNoTracking()
            .Where(c =>
                c.StatusContaId != StatusConta.LiquidadaId &&
                c.StatusContaId != StatusConta.CanceladaId &&
                c.DataVencimento <= dataFinal)
            .SumAsync(c => (decimal?)c.ValorLiquido, cancellationToken) ?? 0m;
    }

    private async Task<decimal> CalcularTotalPendenteContasReceberAsync(DateOnly dataFinal, CancellationToken cancellationToken)
    {
        return await dbContext.ContasReceber
            .AsNoTracking()
            .Where(c =>
                c.StatusContaId != StatusConta.LiquidadaId &&
                c.StatusContaId != StatusConta.CanceladaId &&
                c.DataVencimento <= dataFinal)
            .SumAsync(c => (decimal?)c.ValorLiquido, cancellationToken) ?? 0m;
    }

    private async Task<IReadOnlyList<DashboardContaResumoResponse>> CarregarContasVencidasAsync(
        DateOnly dataReferencia,
        IReadOnlyDictionary<Guid, string> pessoas,
        CancellationToken cancellationToken)
    {
        var contasPagar = await dbContext.ContasPagar
            .AsNoTracking()
            .Where(c =>
                c.StatusContaId != StatusConta.LiquidadaId &&
                c.StatusContaId != StatusConta.CanceladaId &&
                c.StatusContaId != StatusConta.EmFaturaId &&
                c.DataVencimento < dataReferencia)
            .OrderBy(c => c.DataVencimento)
            .Take(5)
            .Select(c => new { c.Id, c.Descricao, PessoaId = c.RecebedorId, c.DataVencimento, c.ValorLiquido })
            .ToListAsync(cancellationToken);

        var contasReceber = await dbContext.ContasReceber
            .AsNoTracking()
            .Where(c =>
                c.StatusContaId != StatusConta.LiquidadaId &&
                c.StatusContaId != StatusConta.CanceladaId &&
                c.DataVencimento < dataReferencia)
            .OrderBy(c => c.DataVencimento)
            .Take(5)
            .Select(c => new { c.Id, c.Descricao, PessoaId = c.PagadorId, c.DataVencimento, c.ValorLiquido })
            .ToListAsync(cancellationToken);

        return contasPagar
            .Select(c => new DashboardContaResumoResponse(
                c.Id,
                "ContaPagar",
                c.Descricao,
                pessoas.GetValueOrDefault(c.PessoaId, string.Empty),
                c.DataVencimento,
                c.ValorLiquido,
                "VENCIDA",
                "Vencida"))
            .Concat(contasReceber.Select(c => new DashboardContaResumoResponse(
                c.Id,
                "ContaReceber",
                c.Descricao,
                pessoas.GetValueOrDefault(c.PessoaId, string.Empty),
                c.DataVencimento,
                c.ValorLiquido,
                "VENCIDA",
                "Vencida")))
            .OrderBy(c => c.DataVencimento)
            .Take(5)
            .ToList();
    }

    private async Task<IReadOnlyList<DashboardContaResumoResponse>> CarregarContasAVencerAsync(
        DateOnly dataInicial,
        DateOnly dataFinal,
        IReadOnlyDictionary<Guid, string> pessoas,
        CancellationToken cancellationToken)
    {
        var contasPagar = await dbContext.ContasPagar
            .AsNoTracking()
            .Where(c =>
                c.StatusContaId != StatusConta.LiquidadaId &&
                c.StatusContaId != StatusConta.CanceladaId &&
                c.StatusContaId != StatusConta.EmFaturaId &&
                c.DataVencimento >= dataInicial &&
                c.DataVencimento <= dataFinal)
            .OrderBy(c => c.DataVencimento)
            .Take(10)
            .Select(c => new { c.Id, c.Descricao, PessoaId = c.RecebedorId, c.DataVencimento, c.ValorLiquido, c.StatusContaId })
            .ToListAsync(cancellationToken);

        var contasReceber = await dbContext.ContasReceber
            .AsNoTracking()
            .Where(c =>
                c.StatusContaId != StatusConta.LiquidadaId &&
                c.StatusContaId != StatusConta.CanceladaId &&
                c.DataVencimento >= dataInicial &&
                c.DataVencimento <= dataFinal)
            .OrderBy(c => c.DataVencimento)
            .Take(10)
            .Select(c => new { c.Id, c.Descricao, PessoaId = c.PagadorId, c.DataVencimento, c.ValorLiquido, c.StatusContaId })
            .ToListAsync(cancellationToken);

        return contasPagar
            .Select(c => MapearContaResumo(c.Id, "ContaPagar", c.Descricao, c.PessoaId, c.DataVencimento, c.ValorLiquido, c.StatusContaId, pessoas))
            .Concat(contasReceber.Select(c => MapearContaResumo(c.Id, "ContaReceber", c.Descricao, c.PessoaId, c.DataVencimento, c.ValorLiquido, c.StatusContaId, pessoas)))
            .OrderBy(c => c.DataVencimento)
            .Take(10)
            .ToList();
    }

    private static DashboardContaResumoResponse MapearContaResumo(
        Guid id,
        string tipoLancamento,
        string descricao,
        Guid pessoaId,
        DateOnly dataVencimento,
        decimal valorLiquido,
        Guid statusContaId,
        IReadOnlyDictionary<Guid, string> pessoas)
    {
        var status = StatusContasLookup.GetValueOrDefault(statusContaId, new StatusContaInfo(string.Empty, string.Empty));

        return new DashboardContaResumoResponse(
            id,
            tipoLancamento,
            descricao,
            pessoas.GetValueOrDefault(pessoaId, string.Empty),
            dataVencimento,
            valorLiquido,
            status.Codigo,
            status.Nome);
    }

    private async Task<IReadOnlyList<DashboardMovimentacaoResumoResponse>> CarregarMovimentacoesRecentesAsync(
        DateOnly dataReferencia,
        CancellationToken cancellationToken)
    {
        var movimentacoes = await dbContext.MovimentacoesFinanceiras
            .AsNoTracking()
            .Where(m =>
                m.Natureza == NaturezaMovimentacao.Realizada &&
                m.StatusMovimentacaoId != StatusMovimentacao.CanceladaId &&
                m.DataMovimentacao <= dataReferencia)
            .OrderByDescending(m => m.DataMovimentacao)
            .ThenByDescending(m => m.CreatedAtUtc)
            .Take(10)
            .ToListAsync(cancellationToken);

        return movimentacoes.Select(m => new DashboardMovimentacaoResumoResponse(
            m.Id,
            m.DataMovimentacao,
            MapearTipoMovimentacao(m.Tipo),
            MapearNaturezaMovimentacao(m.Natureza),
            m.Valor,
            TruncarObservacao(m.Observacao),
            m.ContaPagarId,
            m.ContaReceberId,
            m.FaturaCartaoId)).ToList();
    }

    private async Task<IReadOnlyDictionary<Guid, string>> CarregarPessoasAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Pessoas
            .AsNoTracking()
            .ToDictionaryAsync(p => p.Id, p => p.Nome, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, ContaGerencialInfo>> CarregarContasGerenciaisAsync(CancellationToken cancellationToken)
    {
        return await dbContext.ContasGerenciais
            .AsNoTracking()
            .ToDictionaryAsync(
                c => c.Id,
                c => new ContaGerencialInfo(c.Codigo, c.Descricao, c.Tipo),
                cancellationToken);
    }

    /// <summary>
    /// Resumo por responsável: quem causou despesas/receitas no período (recorte por
    /// DataEmissao, contas não canceladas). Compras de cartão entram nas despesas
    /// com abertura própria em TotalDespesasCartao.
    /// </summary>
    public async Task<DashboardResponsavelResumoResponse> ObterResumoPorResponsavelAsync(
        DashboardResponsavelQueryRequest query,
        CancellationToken cancellationToken)
    {
        var (dataInicial, dias, _) = ResolverJanela(query.MesReferencia, query.DataInicial, query.Dias);
        var dataFinal = dataInicial.AddDays(dias - 1);

        var pessoas = await CarregarPessoasAsync(cancellationToken);

        var despesas = await dbContext.ContasPagar
            .AsNoTracking()
            .Where(c =>
                c.StatusContaId != StatusConta.CanceladaId &&
                c.DataEmissao >= dataInicial &&
                c.DataEmissao <= dataFinal)
            .GroupBy(c => c.ResponsavelCompraId)
            .Select(g => new
            {
                ResponsavelId = g.Key,
                Total = g.Sum(c => c.ValorLiquido),
                TotalCartao = g.Sum(c => c.CartaoId.HasValue ? c.ValorLiquido : 0m),
                Quantidade = g.Count()
            })
            .ToListAsync(cancellationToken);

        var receitas = await dbContext.ContasReceber
            .AsNoTracking()
            .Where(c =>
                c.StatusContaId != StatusConta.CanceladaId &&
                c.DataEmissao >= dataInicial &&
                c.DataEmissao <= dataFinal)
            .GroupBy(c => c.ResponsavelId)
            .Select(g => new
            {
                ResponsavelId = g.Key,
                Total = g.Sum(c => c.ValorLiquido),
                Quantidade = g.Count()
            })
            .ToListAsync(cancellationToken);

        var porResponsavel = new Dictionary<Guid, (decimal Despesas, decimal Cartao, decimal Receitas, int Quantidade)>();

        foreach (var grupo in despesas)
        {
            porResponsavel[grupo.ResponsavelId ?? Guid.Empty] = (grupo.Total, grupo.TotalCartao, 0m, grupo.Quantidade);
        }

        foreach (var grupo in receitas)
        {
            var chave = grupo.ResponsavelId ?? Guid.Empty;
            var atual = porResponsavel.GetValueOrDefault(chave);
            porResponsavel[chave] = (atual.Despesas, atual.Cartao, atual.Receitas + grupo.Total, atual.Quantidade + grupo.Quantidade);
        }

        var itens = porResponsavel
            .Select(par => new DashboardResponsavelItemResponse(
                par.Key == Guid.Empty ? null : par.Key,
                par.Key == Guid.Empty
                    ? "Sem responsável"
                    : pessoas.GetValueOrDefault(par.Key, "Pessoa removida"),
                decimal.Round(par.Value.Despesas, 2, MidpointRounding.AwayFromZero),
                decimal.Round(par.Value.Cartao, 2, MidpointRounding.AwayFromZero),
                decimal.Round(par.Value.Receitas, 2, MidpointRounding.AwayFromZero),
                decimal.Round(par.Value.Receitas - par.Value.Despesas, 2, MidpointRounding.AwayFromZero),
                par.Value.Quantidade))
            .OrderByDescending(item => item.TotalDespesas)
            .ToList();

        return new DashboardResponsavelResumoResponse(
            dataInicial,
            dias,
            decimal.Round(itens.Sum(item => item.TotalDespesas), 2, MidpointRounding.AwayFromZero),
            decimal.Round(itens.Sum(item => item.TotalReceitas), 2, MidpointRounding.AwayFromZero),
            itens);
    }
    private static (DateOnly DataInicial, int Dias, bool MesReferenciaEhMesAtual) ResolverJanela(
        string? mesReferencia,
        DateOnly? dataInicial,
        int dias)
    {
        if (!string.IsNullOrWhiteSpace(mesReferencia))
        {
            var inicioMes = ParseMesReferencia(mesReferencia);
            var hoje = DateOnly.FromDateTime(DateTime.UtcNow);

            return (
                inicioMes,
                DateTime.DaysInMonth(inicioMes.Year, inicioMes.Month),
                inicioMes.Year == hoje.Year && inicioMes.Month == hoje.Month);
        }

        if (dias < 1)
        {
            throw ValidationExceptionFactory.Create("Dias", "Quantidade de dias deve ser maior que zero.");
        }

        return (dataInicial ?? DateOnly.FromDateTime(DateTime.UtcNow), dias, false);
    }

    private static DateOnly ParseMesReferencia(string mesReferencia)
    {
        if (!DateOnly.TryParseExact(
                $"{mesReferencia.Trim()}-01",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var inicioMes))
        {
            throw ValidationExceptionFactory.Create("MesReferencia", "Mês de referência inválido. Use o formato yyyy-MM.");
        }

        return inicioMes;
    }

    private static TipoContaGerencial? ParseTipoContaGerencial(string? tipo)
    {
        if (string.IsNullOrWhiteSpace(tipo))
        {
            return null;
        }

        if (Enum.TryParse<TipoContaGerencial>(tipo, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        throw ValidationExceptionFactory.Create("Tipo", "Tipo de conta gerencial inválido. Use Receita ou Despesa.");
    }

    private static string? TruncarObservacao(string? observacao)
    {
        if (string.IsNullOrWhiteSpace(observacao))
            return null;

        if (observacao.Length <= MaxObservacaoLength)
            return observacao;

        return $"{observacao[..MaxObservacaoLength]}...";
    }

    private static Contracts.Financeiro.Movimentacoes.TipoMovimentacaoResponse MapearTipoMovimentacao(TipoMovimentacao tipo) =>
        tipo switch
        {
            TipoMovimentacao.Entrada => Contracts.Financeiro.Movimentacoes.TipoMovimentacaoResponse.Entrada,
            TipoMovimentacao.Saida => Contracts.Financeiro.Movimentacoes.TipoMovimentacaoResponse.Saida,
            _ => throw new ArgumentOutOfRangeException(nameof(tipo))
        };

    private static Contracts.Financeiro.Movimentacoes.NaturezaMovimentacaoResponse MapearNaturezaMovimentacao(NaturezaMovimentacao natureza) =>
        natureza switch
        {
            NaturezaMovimentacao.Realizada => Contracts.Financeiro.Movimentacoes.NaturezaMovimentacaoResponse.Realizada,
            NaturezaMovimentacao.Prevista => Contracts.Financeiro.Movimentacoes.NaturezaMovimentacaoResponse.Prevista,
            _ => throw new ArgumentOutOfRangeException(nameof(natureza))
        };

    private sealed record StatusContaInfo(string Codigo, string Nome);

    private sealed record ContaGerencialInfo(string? Codigo, string Descricao, TipoContaGerencial Tipo);

    private sealed record ContaJanelaInfo(
        Guid Id,
        string TipoLancamento,
        string Descricao,
        DateOnly DataVencimento,
        decimal ValorLiquido,
        Guid StatusContaId,
        Guid PessoaId,
        Guid? ResponsavelId,
        Guid? RegraRecorrenciaId,
        OrigemLancamento Origem);

    private sealed record ContaRecorrenciaInfo(
        Guid Id,
        Guid RegraId,
        DateOnly DataVencimento,
        decimal ValorLiquido,
        string Descricao,
        OrigemLancamento Origem,
        Guid PessoaId,
        Guid? ResponsavelId);

    private sealed record RecorrenciaProjetada(
        Guid RegraId,
        DateOnly Data,
        TipoMovimentacao Tipo,
        decimal Valor,
        string Descricao,
        Guid PessoaId,
        Guid? ResponsavelId,
        Guid ContaTemplateId,
        bool EhContaPagar);

    private sealed record ImportacaoCompraInfo(
        Guid Id,
        string Descricao,
        decimal Valor,
        DateOnly DataCompra,
        DateOnly DataVencimento,
        TipoMovimentacao Tipo,
        bool Recorrente,
        ParcelamentoCompraCartaoInfo? Parcelamento,
        string? SerieRecorrenteKey,
        string? SerieParcelamentoKey,
        Guid? ContaGerencialId,
        Guid? ResponsavelId);

    internal sealed record RateioLancamentoInfo(
        Guid LancamentoId,
        string TipoLancamento,
        string Descricao,
        Guid PessoaId,
        DateOnly DataEmissao,
        DateOnly DataVencimento,
        decimal ValorLancamento,
        decimal ValorRateio,
        Guid StatusContaId,
        Guid ContaGerencialId);

    private sealed record PrevisaoItem(
        string TipoReferencia,
        Guid ReferenciaId,
        DateOnly Data,
        TipoMovimentacao Tipo,
        DashboardCentralPrevisaoOrigem Origem,
        DashboardCentralPrevisaoStatus Status,
        string Descricao,
        decimal Valor,
        string? PessoaNome,
        string? ResponsavelNome,
        Guid? ContaGerencialId);
}
