using System.Globalization;
using System.Text.RegularExpressions;

namespace ControleFinanceiro.Application.Financeiro.Importacao;

/// <summary>
/// Parser de arquivos OFX (Open Financial Exchange) — formato padrão dos bancos brasileiros.
/// Suporta OFX SGML (legado, sem XML declaration) e OFX XML moderno.
/// </summary>
public static partial class OfxFaturaParser
{
    // OFX SGML: <DTPOSTED>20260601120000[-03:00]
    [GeneratedRegex(@"<DTPOSTED>(\d{8})")]
    private static partial Regex DtPostedRegex();

    [GeneratedRegex(@"<TRNAMT>([-\d.]+)")]
    private static partial Regex TrnAmtRegex();

    [GeneratedRegex(@"<MEMO>([^\r\n<]+)")]
    private static partial Regex MemoRegex();

    [GeneratedRegex(@"<NAME>([^\r\n<]+)")]
    private static partial Regex NameRegex();

    [GeneratedRegex(@"<TRNTYPE>([^\r\n<]+)")]
    private static partial Regex TrnTypeRegex();

    [GeneratedRegex(@"<STMTTRN>(.*?)</STMTTRN>", RegexOptions.Singleline)]
    private static partial Regex StmtTrnRegex();

    public static CsvFaturaParser.ParseResult Parse(string conteudo)
    {
        if (string.IsNullOrWhiteSpace(conteudo))
            return new CsvFaturaParser.ParseResult([], "Arquivo OFX vazio.");

        var itens = new List<CsvFaturaItem>();

        // OFX SGML não tem tags de fechamento no cabeçalho mas usa <STMTTRN>...</STMTTRN>
        // Normalizar: se não encontrar </STMTTRN>, tentar parsear linha a linha
        var matches = StmtTrnRegex().Matches(conteudo);
        if (matches.Count > 0)
        {
            foreach (Match match in matches)
            {
                var block = match.Value;
                var item = ParseBlock(block);
                if (item is not null) itens.Add(item);
            }
        }
        else
        {
            // Fallback: SGML sem fechamento — parsear sequencialmente
            itens.AddRange(ParseSgmlSequential(conteudo));
        }

        if (itens.Count == 0)
            return new CsvFaturaParser.ParseResult([], "Nenhuma transação de débito encontrada no OFX.");

        return new CsvFaturaParser.ParseResult(itens, null);
    }

    private static CsvFaturaItem? ParseBlock(string block)
    {
        var trnType = TrnTypeRegex().Match(block).Groups[1].Value.Trim().ToUpperInvariant();
        // Incluir apenas débitos (despesas)
        if (trnType is not ("DEBIT" or "PAYMENT" or "CHECK" or "ATM" or "POS" or "XFER"))
            return null;

        var dtMatch = DtPostedRegex().Match(block);
        if (!dtMatch.Success) return null;
        if (!TryParseOfxDate(dtMatch.Groups[1].Value, out var data)) return null;

        var amtMatch = TrnAmtRegex().Match(block);
        if (!amtMatch.Success) return null;
        if (!decimal.TryParse(amtMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var valor))
            return null;
        // OFX: débitos são negativos; tomamos o absoluto
        valor = Math.Abs(valor);
        if (valor <= 0) return null;

        var descricao = MemoRegex().Match(block).Groups[1].Value.Trim();
        if (string.IsNullOrWhiteSpace(descricao))
            descricao = NameRegex().Match(block).Groups[1].Value.Trim();
        if (string.IsNullOrWhiteSpace(descricao)) return null;

        if (descricao.Length > 100) descricao = descricao[..100];
        return new CsvFaturaItem(data, descricao, valor);
    }

    private static IEnumerable<CsvFaturaItem> ParseSgmlSequential(string conteudo)
    {
        // Divide em "blocos" começando em <STMTTRN>
        var sections = conteudo.Split("<STMTTRN>", StringSplitOptions.RemoveEmptyEntries);
        foreach (var section in sections.Skip(1)) // skip cabeçalho
        {
            var item = ParseBlock("<STMTTRN>" + section);
            if (item is not null) yield return item;
        }
    }

    private static bool TryParseOfxDate(string s, out DateOnly result)
    {
        result = default;
        // Formato: YYYYMMDD (pode ter HHMMSS e timezone depois)
        if (s.Length < 8) return false;
        return DateOnly.TryParseExact(s[..8], "yyyyMMdd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
    }
}
