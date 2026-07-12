using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Contracts.Financeiro.Investimentos;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ControleFinanceiro.Application.Financeiro.Investimentos;

public sealed class InvestimentoAppService(IAppDbContext dbContext, IMemoryCache cache)
{
    private static readonly Dictionary<TipoInvestimento, string> TipoLabels = new()
    {
        [TipoInvestimento.RendaFixa] = "Renda Fixa",
        [TipoInvestimento.RendaVariavel] = "Renda Variável",
        [TipoInvestimento.FundoImobiliario] = "Fundo Imobiliário",
        [TipoInvestimento.Criptomoeda] = "Criptomoeda",
        [TipoInvestimento.Outro] = "Outro"
    };

    private static readonly Dictionary<LiquidezInvestimento, string> LiquidezLabels = new()
    {
        [LiquidezInvestimento.Diaria] = "Diária",
        [LiquidezInvestimento.Vencimento] = "No vencimento",
        [LiquidezInvestimento.Iliquido] = "Ilíquido"
    };

    public async Task<InvestimentoResumoResponse> CriarAsync(
        CriarInvestimentoRequest request,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.ContasBancarias.AnyAsync(x => x.Id == request.ContaBancariaVinculadaId, cancellationToken))
            throw ValidationExceptionFactory.Create("ContaBancariaVinculadaId", "Conta bancária não encontrada.");

        var investimento = Investimento.Criar(
            request.Nome,
            request.Emissor,
            request.Tipo,
            request.Liquidez,
            request.ValorInvestido,
            request.DataAplicacao,
            request.DataVencimento,
            request.TaxaAnual,
            request.ContaBancariaVinculadaId);

        dbContext.Investimentos.Add(investimento);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await ProjetarAsync(investimento.Id, cancellationToken)
            ?? throw new InvalidOperationException("Falha ao carregar investimento criado.");
    }

    public async Task<InvestimentoResumoResponse> AtualizarAsync(
        Guid id,
        AtualizarInvestimentoRequest request,
        CancellationToken cancellationToken)
    {
        var investimento = await dbContext.Investimentos.FindAsync([id], cancellationToken)
            ?? throw ValidationExceptionFactory.Create("Id", "Investimento não encontrado.");

        investimento.Atualizar(
            request.Nome,
            request.Emissor,
            request.Tipo,
            request.Liquidez,
            request.DataVencimento,
            request.TaxaAnual);

        await dbContext.SaveChangesAsync(cancellationToken);
        return (await ProjetarAsync(id, cancellationToken))!;
    }

    public async Task<InvestimentoResumoResponse> AtualizarValorAtualAsync(
        Guid id,
        AtualizarValorAtualRequest request,
        CancellationToken cancellationToken)
    {
        var investimento = await dbContext.Investimentos.FindAsync([id], cancellationToken)
            ?? throw ValidationExceptionFactory.Create("Id", "Investimento não encontrado.");

        investimento.AtualizarValorAtual(request.ValorAtual);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (await ProjetarAsync(id, cancellationToken))!;
    }

    public async Task<InvestimentoResumoResponse> EncerrarAsync(
        Guid id,
        EncerrarInvestimentoRequest request,
        CancellationToken cancellationToken)
    {
        var investimento = await dbContext.Investimentos.FindAsync([id], cancellationToken)
            ?? throw ValidationExceptionFactory.Create("Id", "Investimento não encontrado.");

        investimento.Encerrar(request.ValorResgate);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (await ProjetarAsync(id, cancellationToken))!;
    }

    public async Task<InvestimentoListResponse> ListarAsync(
        InvestimentoListQuery query,
        CancellationToken cancellationToken)
    {
        var q = dbContext.Investimentos.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim().ToLower();
            q = q.Where(x => x.Nome.ToLower().Contains(s) || (x.Emissor != null && x.Emissor.ToLower().Contains(s)));
        }

        if (query.Tipo.HasValue)
            q = q.Where(x => x.Tipo == query.Tipo.Value);

        if (query.Encerrado.HasValue)
            q = q.Where(x => x.Encerrado == query.Encerrado.Value);

        if (query.ContaBancariaVinculadaId.HasValue)
            q = q.Where(x => x.ContaBancariaVinculadaId == query.ContaBancariaVinculadaId.Value);

        var total = await q.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling((double)total / query.PageSize);

        var raw = await q
            .OrderByDescending(x => x.DataAplicacao)
            .ThenBy(x => x.Nome)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Join(dbContext.ContasBancarias,
                inv => inv.ContaBancariaVinculadaId,
                cb => cb.Id,
                (inv, cb) => new { inv, cb })
            .Select(x => new
            {
                x.inv.Id, x.inv.Nome, x.inv.Emissor, x.inv.Tipo, x.inv.Liquidez,
                x.inv.ValorInvestido, x.inv.ValorAtual, x.inv.DataAplicacao,
                x.inv.DataVencimento, x.inv.TaxaAnual, x.inv.ContaBancariaVinculadaId,
                ContaBancariaNome = x.cb.Nome, x.inv.Encerrado, x.inv.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var items = raw.Select(x => new InvestimentoResumoResponse(
            x.Id, x.Nome, x.Emissor, x.Tipo, TipoLabels[x.Tipo], x.Liquidez, LiquidezLabels[x.Liquidez],
            x.ValorInvestido, x.ValorAtual, x.ValorAtual - x.ValorInvestido,
            x.ValorInvestido > 0 ? ((x.ValorAtual - x.ValorInvestido) / x.ValorInvestido) * 100m : 0m,
            x.DataAplicacao, x.DataVencimento, x.TaxaAnual, x.ContaBancariaVinculadaId,
            x.ContaBancariaNome, x.Encerrado, x.CreatedAtUtc)).ToList();

        return new InvestimentoListResponse(items, query.Page, query.PageSize, total, totalPages);
    }

    public async Task<InvestimentoResumoResponse?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
        => await ProjetarAsync(id, cancellationToken);

    public async Task<IndicadoresBcbResponse> ObterIndicadoresBcbAsync(CancellationToken cancellationToken)
    {
        const string cacheKey = "bcb:indicadores";

        if (cache.TryGetValue(cacheKey, out IndicadoresBcbResponse? cached) && cached is not null)
            return cached;

        var resultado = await BuscarIndicadoresBcbAsync(cancellationToken);

        cache.Set(cacheKey, resultado, TimeSpan.FromHours(6));
        return resultado;
    }

    private static async Task<IndicadoresBcbResponse> BuscarIndicadoresBcbAsync(CancellationToken cancellationToken)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        decimal? selic = null;
        decimal? cdi = null;
        decimal? ipca = null;

        try
        {
            // SELIC anualizada (série 4189)
            var selicJson = await http.GetStringAsync(
                "https://api.bcb.gov.br/dados/serie/bcdata.sgs.4189/dados/ultimos/1?formato=json",
                cancellationToken);
            selic = ParseBcbValue(selicJson);
        }
        catch { /* ignora falha pontual */ }

        try
        {
            // CDI anualizado (série 4392)
            var cdiJson = await http.GetStringAsync(
                "https://api.bcb.gov.br/dados/serie/bcdata.sgs.4392/dados/ultimos/1?formato=json",
                cancellationToken);
            cdi = ParseBcbValue(cdiJson);
        }
        catch { /* ignora falha pontual */ }

        try
        {
            // IPCA acumulado 12 meses (série 13522)
            var ipcaJson = await http.GetStringAsync(
                "https://api.bcb.gov.br/dados/serie/bcdata.sgs.13522/dados/ultimos/1?formato=json",
                cancellationToken);
            ipca = ParseBcbValue(ipcaJson);
        }
        catch { /* ignora falha pontual */ }

        return new IndicadoresBcbResponse(selic, cdi, ipca, DateTime.UtcNow);
    }

    private static decimal? ParseBcbValue(string json)
    {
        // Formato: [{"data":"01/01/2024","valor":"10.75"}]
        var startIdx = json.IndexOf("\"valor\":\"", StringComparison.Ordinal);
        if (startIdx < 0) return null;
        startIdx += 9;
        var endIdx = json.IndexOf('"', startIdx);
        if (endIdx < 0) return null;
        var raw = json[startIdx..endIdx].Replace(',', '.');
        return decimal.TryParse(raw, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private async Task<InvestimentoResumoResponse?> ProjetarAsync(Guid id, CancellationToken cancellationToken)
    {
        var raw = await dbContext.Investimentos
            .Where(x => x.Id == id)
            .Join(dbContext.ContasBancarias,
                inv => inv.ContaBancariaVinculadaId,
                cb => cb.Id,
                (inv, cb) => new { inv, cb })
            .Select(x => new
            {
                x.inv.Id, x.inv.Nome, x.inv.Emissor, x.inv.Tipo, x.inv.Liquidez,
                x.inv.ValorInvestido, x.inv.ValorAtual, x.inv.DataAplicacao,
                x.inv.DataVencimento, x.inv.TaxaAnual, x.inv.ContaBancariaVinculadaId,
                ContaBancariaNome = x.cb.Nome, x.inv.Encerrado, x.inv.CreatedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (raw is null) return null;

        return new InvestimentoResumoResponse(
            raw.Id, raw.Nome, raw.Emissor, raw.Tipo, TipoLabels[raw.Tipo], raw.Liquidez, LiquidezLabels[raw.Liquidez],
            raw.ValorInvestido, raw.ValorAtual, raw.ValorAtual - raw.ValorInvestido,
            raw.ValorInvestido > 0 ? ((raw.ValorAtual - raw.ValorInvestido) / raw.ValorInvestido) * 100m : 0m,
            raw.DataAplicacao, raw.DataVencimento, raw.TaxaAnual, raw.ContaBancariaVinculadaId,
            raw.ContaBancariaNome, raw.Encerrado, raw.CreatedAtUtc);
    }
}
