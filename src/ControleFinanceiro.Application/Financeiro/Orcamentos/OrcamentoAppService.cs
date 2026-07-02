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
    DashboardDbHelpers dashboardDbHelpers)
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
        var filhosPorContaPai = contasDespesa
            .Where(c => c.ContaPaiId.HasValue)
            .GroupBy(c => c.ContaPaiId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(conta => conta.Id).ToArray());

        var rateios = await dashboardDbHelpers.CarregarRateiosPorEmissaoAsync(inicioMes, fimMes, cancellationToken);
        var realizadoDiretoPorConta = rateios
            .Where(r => r.TipoLancamento == "ContaPagar")
            .GroupBy(r => r.ContaGerencialId)
            .ToDictionary(
                g => g.Key,
                g => decimal.Round(g.Sum(r => r.ValorRateio), 2, MidpointRounding.AwayFromZero));

        var metaCache = new Dictionary<Guid, decimal?>();
        var realizadoCache = new Dictionary<Guid, decimal>();

        decimal? CalcularMeta(Guid contaId)
        {
            if (metaCache.TryGetValue(contaId, out var metaCalculada))
            {
                return metaCalculada;
            }

            if (!filhosPorContaPai.TryGetValue(contaId, out var filhosIds) || filhosIds.Length == 0)
            {
                metaCalculada = metasPorConta.GetValueOrDefault(contaId)?.ValorMeta;
                metaCache[contaId] = metaCalculada;
                return metaCalculada;
            }

            var possuiMeta = false;
            var totalMeta = 0m;

            foreach (var filhoId in filhosIds)
            {
                var metaFilho = CalcularMeta(filhoId);
                if (!metaFilho.HasValue)
                {
                    continue;
                }

                possuiMeta = true;
                totalMeta += metaFilho.Value;
            }

            metaCalculada = possuiMeta
                ? decimal.Round(totalMeta, 2, MidpointRounding.AwayFromZero)
                : null;

            metaCache[contaId] = metaCalculada;
            return metaCalculada;
        }

        decimal CalcularRealizado(Guid contaId)
        {
            if (realizadoCache.TryGetValue(contaId, out var realizadoCalculado))
            {
                return realizadoCalculado;
            }

            realizadoCalculado = realizadoDiretoPorConta.GetValueOrDefault(contaId);

            if (filhosPorContaPai.TryGetValue(contaId, out var filhosIds))
            {
                foreach (var filhoId in filhosIds)
                {
                    realizadoCalculado += CalcularRealizado(filhoId);
                }
            }

            realizadoCalculado = decimal.Round(realizadoCalculado, 2, MidpointRounding.AwayFromZero);
            realizadoCache[contaId] = realizadoCalculado;
            return realizadoCalculado;
        }

        var itens = contasDespesa
            .Select(conta =>
            {
                var meta = CalcularMeta(conta.Id);
                var realizado = CalcularRealizado(conta.Id);
                var aceitaLancamentos = !filhosPorContaPai.ContainsKey(conta.Id);

                return new
                {
                    Conta = conta,
                    Meta = meta,
                    Realizado = realizado,
                    AceitaLancamentos = aceitaLancamentos
                };
            })
            .Where(x =>
                x.Conta.Ativo ||
                x.Meta.HasValue ||
                x.Realizado > 0m)
            .Select(x => new OrcamentoItemResponse(
                metasPorConta.GetValueOrDefault(x.Conta.Id)?.Id,
                x.Conta.Id,
                x.Conta.ContaPaiId,
                x.Conta.Codigo,
                x.Conta.Descricao,
                x.Meta,
                x.Realizado,
                CalcularPercentual(x.Realizado, x.Meta),
                EstaEstourado(x.Realizado, x.Meta),
                x.AceitaLancamentos))
            .OrderBy(item => item.ContaGerencialCodigo ?? string.Empty)
            .ThenBy(item => item.ContaGerencialDescricao)
            .ToList();

        var totalMeta = decimal.Round(
            itens.Where(item => item.ContaPaiId is null).Sum(item => item.ValorMeta ?? 0m),
            2,
            MidpointRounding.AwayFromZero);
        var totalRealizado = decimal.Round(
            itens.Where(item => item.ContaPaiId is null).Sum(item => item.ValorRealizado),
            2,
            MidpointRounding.AwayFromZero);

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
            throw ValidationExceptionFactory.Create("ContaGerencialId", "Conta gerencial e obrigatoria.");
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
            throw ValidationExceptionFactory.Create("ContaGerencialId", "Conta gerencial nao encontrada.");
        }

        if (contaGerencial.Tipo != TipoContaGerencial.Despesa)
        {
            throw ValidationExceptionFactory.Create("ContaGerencialId", "Somente contas gerenciais de despesa aceitam meta de orcamento.");
        }

        var possuiFilhos = await dbContext.ContasGerenciais
            .AsNoTracking()
            .AnyAsync(c => c.ContaPaiId == request.ContaGerencialId, cancellationToken);

        if (possuiFilhos)
        {
            throw ValidationExceptionFactory.Create(
                "ContaGerencialId",
                "Contas pai do orcamento sao calculadas automaticamente pela soma das contas filhas.");
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
            throw ValidationExceptionFactory.Create("Competencia", "Competencia e obrigatoria. Use o formato yyyy-MM.");
        }

        if (!DateOnly.TryParseExact(
                $"{competencia.Trim()}-01",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var inicioMes))
        {
            throw ValidationExceptionFactory.Create("Competencia", "Competencia invalida. Use o formato yyyy-MM.");
        }

        return inicioMes;
    }
}
