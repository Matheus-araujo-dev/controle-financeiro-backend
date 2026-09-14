using System.Globalization;
using System.Text;
using System.Xml.Linq;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Contracts.Conciliacao;
using ControleFinanceiro.Domain.Conciliacao;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Conciliacao;

public sealed class ConciliacaoAppService(IAppDbContext dbContext)
{
    private const int MaxFileBytes = 5 * 1024 * 1024;

    public async Task<ConciliacaoDetalheResponse?> IniciarAsync(
        Guid contaBancariaId,
        string nomeArquivo,
        Stream conteudo,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.ContasBancarias.AnyAsync(c => c.Id == contaBancariaId, cancellationToken))
            return null;

        var buffer = new MemoryStream();
        await conteudo.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length == 0 || buffer.Length > MaxFileBytes)
            throw ValidationExceptionFactory.Create("Arquivo", "O arquivo deve ter conteudo e no maximo 5 MB.");
        buffer.Position = 0;

        var extension = Path.GetExtension(nomeArquivo).ToLowerInvariant();
        var formato = extension switch
        {
            ".ofx" => FormatoArquivo.Ofx,
            ".csv" => FormatoArquivo.Csv,
            _ => throw ValidationExceptionFactory.Create("Arquivo", "Formato nao suportado. Use OFX ou CSV.")
        };

        var linhas = formato == FormatoArquivo.Ofx
            ? ParseOfx(buffer)
            : ParseCsv(buffer);

        if (linhas.Count == 0)
            throw ValidationExceptionFactory.Create("Arquivo", "Nenhuma transacao encontrada no arquivo.");

        var dataInicio = linhas.Min(l => l.Data);
        var dataFim = linhas.Max(l => l.Data);
        var itens = linhas.Select(l => ItemConciliacao.Criar(l.Data, l.Descricao, l.Valor, l.Documento)).ToList();

        var conciliacao = Domain.Conciliacao.Conciliacao.Criar(nomeArquivo, formato, contaBancariaId, dataInicio, dataFim, itens);
        dbContext.Conciliacoes.Add(conciliacao);

        await SugerirMatchesAsync(conciliacao, contaBancariaId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapDetalhe(conciliacao);
    }

    public async Task<IReadOnlyCollection<ConciliacaoResumoResponse>> ListarAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Conciliacoes.AsNoTracking()
            .OrderByDescending(c => c.CreatedAtUtc)
            .Take(50)
            .Select(c => new ConciliacaoResumoResponse(
                c.Id, c.NomeArquivo, c.Formato.ToString(), c.ContaBancariaId,
                c.DataInicio, c.DataFim, c.TotalItens, c.ItensConciliados,
                c.Status.ToString(), c.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<ConciliacaoDetalheResponse?> ObterAsync(Guid id, CancellationToken cancellationToken)
    {
        var conciliacao = await dbContext.Conciliacoes
            .Include(c => c.Itens)
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        return conciliacao is null ? null : MapDetalhe(conciliacao);
    }

    public async Task<bool> ConciliarItemAsync(Guid conciliacaoId, Guid itemId, ConciliarItemRequest request, CancellationToken cancellationToken)
    {
        var conciliacao = await dbContext.Conciliacoes
            .Include(c => c.Itens)
            .SingleOrDefaultAsync(c => c.Id == conciliacaoId, cancellationToken);
        if (conciliacao is null) return false;

        conciliacao.ConciliarItem(itemId, request.MovimentacaoId);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> IgnorarItemAsync(Guid conciliacaoId, Guid itemId, CancellationToken cancellationToken)
    {
        var conciliacao = await dbContext.Conciliacoes
            .Include(c => c.Itens)
            .SingleOrDefaultAsync(c => c.Id == conciliacaoId, cancellationToken);
        if (conciliacao is null) return false;

        conciliacao.IgnorarItem(itemId);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task SugerirMatchesAsync(
        Domain.Conciliacao.Conciliacao conciliacao,
        Guid contaBancariaId,
        CancellationToken cancellationToken)
    {
        var movimentacoes = await dbContext.MovimentacoesFinanceiras.AsNoTracking()
            .Where(m => m.ContaBancariaId == contaBancariaId
                && m.DataMovimentacao >= conciliacao.DataInicio.AddDays(-3)
                && m.DataMovimentacao <= conciliacao.DataFim.AddDays(3)
                && m.StatusMovimentacaoId != StatusMovimentacao.CanceladaId)
            .ToListAsync(cancellationToken);

        foreach (var item in conciliacao.Itens)
        {
            var melhorMatch = movimentacoes
                .Select(m => new { Mov = m, Score = CalcularScore(item, m) })
                .Where(x => x.Score >= 0.6m)
                .MaxBy(x => x.Score);

            if (melhorMatch is not null)
                item.DefinirSugestao(melhorMatch.Mov.Id, melhorMatch.Score);
        }
    }

    private static decimal CalcularScore(ItemConciliacao item, MovimentacaoFinanceira mov)
    {
        decimal score = 0;
        if (item.Valor == mov.Valor) score += 0.5m;
        else if (Math.Abs(item.Valor - mov.Valor) < 0.01m) score += 0.4m;

        var diffDias = Math.Abs(item.Data.DayNumber - mov.DataMovimentacao.DayNumber);
        if (diffDias == 0) score += 0.3m;
        else if (diffDias <= 1) score += 0.2m;
        else if (diffDias <= 3) score += 0.1m;

        if (!string.IsNullOrWhiteSpace(mov.Observacao) && !string.IsNullOrWhiteSpace(item.Descricao))
        {
            var descNorm = item.Descricao.ToUpperInvariant();
            var obsNorm = mov.Observacao.ToUpperInvariant();
            if (descNorm.Contains(obsNorm) || obsNorm.Contains(descNorm))
                score += 0.2m;
        }

        return Math.Min(score, 1.0m);
    }

    private static IReadOnlyList<TransacaoExtrato> ParseOfx(MemoryStream buffer)
    {
        var text = Encoding.UTF8.GetString(buffer.ToArray());
        // Strip SGML header for OFX 1.x
        var xmlStart = text.IndexOf("<OFX>", StringComparison.OrdinalIgnoreCase);
        if (xmlStart < 0) return [];

        var xmlText = text[xmlStart..];
        // Close unclosed tags for SGML-style OFX
        xmlText = System.Text.RegularExpressions.Regex.Replace(
            xmlText,
            @"<(\w+)>([^<]+)(?=<(?!/\1))",
            "<$1>$2</$1>");

        XDocument doc;
        try { doc = XDocument.Parse(xmlText); }
        catch { return []; }

        var result = new List<TransacaoExtrato>();
        foreach (var stmtTrn in doc.Descendants().Where(e => e.Name.LocalName.Equals("STMTTRN", StringComparison.OrdinalIgnoreCase)))
        {
            var dtPosted = stmtTrn.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("DTPOSTED", StringComparison.OrdinalIgnoreCase))?.Value;
            var trnAmt = stmtTrn.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("TRNAMT", StringComparison.OrdinalIgnoreCase))?.Value;
            var memo = stmtTrn.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("MEMO", StringComparison.OrdinalIgnoreCase))?.Value;
            var checkNum = stmtTrn.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("CHECKNUM", StringComparison.OrdinalIgnoreCase))?.Value;

            if (dtPosted is null || trnAmt is null) continue;

            if (DateOnly.TryParseExact(dtPosted[..Math.Min(8, dtPosted.Length)], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data)
                && decimal.TryParse(trnAmt, NumberStyles.Any, CultureInfo.InvariantCulture, out var valor))
            {
                result.Add(new TransacaoExtrato(data, memo ?? string.Empty, valor, checkNum));
            }
        }
        return result;
    }

    private static IReadOnlyList<TransacaoExtrato> ParseCsv(MemoryStream buffer)
    {
        var text = Encoding.UTF8.GetString(buffer.ToArray());
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2) return [];

        var result = new List<TransacaoExtrato>();
        // Skip header
        foreach (var line in lines.Skip(1))
        {
            var cols = line.Split(';');
            if (cols.Length < 3) cols = line.Split(',');
            if (cols.Length < 3) continue;

            if (DateOnly.TryParse(cols[0].Trim().Trim('"'), CultureInfo.GetCultureInfo("pt-BR"), out var data)
                && decimal.TryParse(cols[2].Trim().Trim('"').Replace(".", "").Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var valor))
            {
                var descricao = cols[1].Trim().Trim('"');
                var documento = cols.Length > 3 ? cols[3].Trim().Trim('"') : null;
                result.Add(new TransacaoExtrato(data, descricao, valor, string.IsNullOrWhiteSpace(documento) ? null : documento));
            }
        }
        return result;
    }

    private static ConciliacaoDetalheResponse MapDetalhe(Domain.Conciliacao.Conciliacao c) => new(
        c.Id, c.NomeArquivo, c.Formato.ToString(), c.ContaBancariaId,
        c.DataInicio, c.DataFim, c.TotalItens, c.ItensConciliados, c.Status.ToString(),
        c.Itens.Select(i => new ItemConciliacaoResponse(
            i.Id, i.Data, i.Descricao, i.Valor, i.Documento,
            i.StatusItem.ToString(), i.MovimentacaoVinculadaId,
            i.SugestaoMovimentacaoId, i.ScoreSugestao)).ToArray());

    private sealed record TransacaoExtrato(DateOnly Data, string Descricao, decimal Valor, string? Documento);
}