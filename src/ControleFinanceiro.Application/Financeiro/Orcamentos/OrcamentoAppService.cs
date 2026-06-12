using System.Globalization;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Application.Dashboard;
using ControleFinanceiro.Contracts.Financeiro.Orcamentos;
using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Financeiro.Orcamentos;

public sealed class OrcamentoAppService(
    IAppDbContext dbContext,
    DashboardAppService dashboardAppService)
{
    private const decimal LimiteEstouroPercentual = 100m;

    public async Task<OrcamentoCompetenciaResponse> ObterPorCompetenciaAsync(
        OrcamentoQueryRequest query,
        CancellationToken cancellationToken)
    {
        var inicioMes = ParseCompetencia(query.Competencia);
        var fimMes = inicioMes.AddMonths(1).AddDays(-1);
        var competencia = inicioMes.ToString("yyyy-MM", CultureInfo.InvariantCulture);

        var metas = await dbContext.MetasOrcamento
            .AsNoTracking()
            .Where(m => m.Competencia == competencia)
            .ToListAsync(cancellationToken);

        var contasDespesa = await dbContext.ContasGerenciais
            .AsNoTracking()
            .Where(c => c.Tipo == TipoContaGerencial.Despesa)
            .ToListAsync(cancellationToken);

        var metasPorConta = metas.ToDictionary(m => m.ContaGerencialId);

        var rateios = await dashboardAppService.CarregarRateiosPorEmissaoAsync(inicioMes, fimMes, cancellationToken);
        var realizadoPorConta = rateios
            .Where(r => r.TipoLancamento == "ContaPagar")
            .GroupBy(r => r.ContaGerencialId)
            .ToDictionary(
                g => g.Key,
                g => decimal.Round(g.Sum(r => r.ValorRateio), 2, MidpointRounding.AwayFromZero));

        var itens = contasDespesa
            .Where(conta =>
                conta.Ativo ||
                metasPorConta.ContainsKey(conta.Id) ||
                realizadoPorConta.ContainsKey(conta.Id))
            .Select(conta =>
            {
                var meta = metasPorConta.GetValueOrDefault(conta.Id);
                var realizado = realizadoPorConta.GetValueOrDefault(conta.Id);

                return new OrcamentoItemResponse(
                    meta?.Id,
                    conta.Id,
                    conta.Codigo,
                    conta.Descricao,
                    meta?.ValorMeta,
                    realizado,
                    CalcularPercentual(realizado, meta?.ValorMeta),
                    EstaEstourado(realizado, meta?.ValorMeta));
            })
            // Estouradas primeiro; depois por % consumido decrescente (null = sem meta, vai para baixo);
            // depois por maior gasto absoluto; por último por código e descrição.
            .OrderByDescending(item => item.Estourado)
            .ThenByDescending(item => item.PercentualConsumido ?? -1m)
            .ThenByDescending(item => item.ValorRealizado)
            .ThenBy(item => item.ContaGerencialCodigo ?? string.Empty)
            .ThenBy(item => item.ContaGerencialDescricao)
            .ToList();

        var totalMeta = decimal.Round(itens.Sum(item => item.ValorMeta ?? 0m), 2, MidpointRounding.AwayFromZero);
        var totalRealizado = decimal.Round(itens.Sum(item => item.ValorRealizado), 2, MidpointRounding.AwayFromZero);

        return new OrcamentoCompetenciaResponse(
            competencia,
            totalMeta,
            totalRealizado,
            CalcularPercentual(totalRealizado, totalMeta > 0 ? totalMeta : null),
            itens.Any(item => item.Estourado),
            itens);
    }

    public async Task<MetaOrcamentoResponse> UpsertMetaAsync(
        UpsertMetaOrcamentoRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ContaGerencialId == Guid.Empty)
        {
            throw ValidationExceptionFactory.Create("ContaGerencialId", "Conta gerencial é obrigatória.");
        }

        if (request.ValorMeta <= 0)
        {
            throw ValidationExceptionFactory.Create("ValorMeta", "Valor da meta deve ser maior que zero.");
        }

        var inicioMes = ParseCompetencia(request.Competencia);
        var competencia = inicioMes.ToString("yyyy-MM", CultureInfo.InvariantCulture);

        var contaGerencial = await dbContext.ContasGerenciais
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == request.ContaGerencialId, cancellationToken);

        if (contaGerencial is null)
        {
            throw ValidationExceptionFactory.Create("ContaGerencialId", "Conta gerencial não encontrada.");
        }

        if (contaGerencial.Tipo != TipoContaGerencial.Despesa)
        {
            throw ValidationExceptionFactory.Create("ContaGerencialId", "Somente contas gerenciais de despesa aceitam meta de orçamento.");
        }

        var meta = await dbContext.MetasOrcamento
            .SingleOrDefaultAsync(
                m => m.ContaGerencialId == request.ContaGerencialId && m.Competencia == competencia,
                cancellationToken);

        if (meta is null)
        {
            meta = MetaOrcamento.Criar(request.ContaGerencialId, competencia, request.ValorMeta);
            dbContext.MetasOrcamento.Add(meta);
        }
        else
        {
            meta.Atualizar(request.ValorMeta);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new MetaOrcamentoResponse(meta.Id, meta.ContaGerencialId, meta.Competencia, meta.ValorMeta);
    }

    public async Task<bool> RemoverMetaAsync(Guid id, CancellationToken cancellationToken)
    {
        var meta = await dbContext.MetasOrcamento
            .SingleOrDefaultAsync(m => m.Id == id, cancellationToken);

        if (meta is null)
        {
            return false;
        }

        dbContext.MetasOrcamento.Remove(meta);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static decimal? CalcularPercentual(decimal realizado, decimal? meta)
    {
        if (!meta.HasValue || meta.Value <= 0)
        {
            return null;
        }

        return decimal.Round((realizado / meta.Value) * 100m, 2, MidpointRounding.AwayFromZero);
    }

    private static bool EstaEstourado(decimal realizado, decimal? meta)
    {
        var percentual = CalcularPercentual(realizado, meta);
        return percentual.HasValue && percentual.Value > LimiteEstouroPercentual;
    }

    private static DateOnly ParseCompetencia(string? competencia)
    {
        if (string.IsNullOrWhiteSpace(competencia))
        {
            throw ValidationExceptionFactory.Create("Competencia", "Competência é obrigatória. Use o formato yyyy-MM.");
        }

        if (!DateOnly.TryParseExact(
                $"{competencia.Trim()}-01",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var inicioMes))
        {
            throw ValidationExceptionFactory.Create("Competencia", "Competência inválida. Use o formato yyyy-MM.");
        }

        return inicioMes;
    }
}
