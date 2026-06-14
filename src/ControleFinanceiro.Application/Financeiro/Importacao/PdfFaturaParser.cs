using System.Globalization;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace ControleFinanceiro.Application.Financeiro.Importacao;

public static partial class PdfFaturaParser
{
    [GeneratedRegex(@"^\d{2}/\d{2}/\d{4}$|^\d{2}/\d{2}/\d{2}$")]
    private static partial Regex DateRegex();

    [GeneratedRegex(@"^-?\d{1,3}(\.\d{3})*,\d{2}$|^-?\d+,\d{2}$")]
    private static partial Regex ValueRegex();

    public static CsvFaturaParser.ParseResult Parse(Stream stream)
    {
        var itens = new List<CsvFaturaItem>();

        try
        {
            using var doc = PdfDocument.Open(stream);

            foreach (var page in doc.GetPages())
            {
                var words = page.GetWords().ToList();
                if (words.Count == 0) continue;

                var rows = GroupByRow(words);

                foreach (var row in rows)
                {
                    var item = TryParseRow(row);
                    if (item is not null) itens.Add(item);
                }
            }
        }
        catch (Exception)
        {
            return new CsvFaturaParser.ParseResult(
                [],
                "Não foi possível ler o PDF. Verifique se o arquivo não está protegido por senha.");
        }

        string? aviso = itens.Count == 0
            ? "Nenhum lançamento encontrado no PDF. O formato pode não ser suportado — tente exportar como CSV."
            : null;

        return new CsvFaturaParser.ParseResult(itens, aviso);
    }

    // ─── Agrupamento por linha (Y) ──────────────────────────────────────────

    private static List<List<Word>> GroupByRow(IReadOnlyList<Word> words, double tolerance = 2.5)
    {
        // PDF Y cresce para cima; ordena descendente para percorrer de cima para baixo
        var sorted = words.OrderByDescending(w => w.BoundingBox.Bottom).ToList();
        var rows = new List<List<Word>>();
        List<Word>? current = null;
        double currentY = double.MaxValue;

        foreach (var word in sorted)
        {
            double y = word.BoundingBox.Bottom;
            if (current is null || Math.Abs(y - currentY) > tolerance)
            {
                if (current?.Count > 0)
                    rows.Add([.. current.OrderBy(w => w.BoundingBox.Left)]);
                current = [];
                currentY = y;
            }
            current!.Add(word);
        }

        if (current?.Count > 0)
            rows.Add([.. current.OrderBy(w => w.BoundingBox.Left)]);

        return rows;
    }

    // ─── Tentativa de parsear uma linha como transação ──────────────────────

    private static CsvFaturaItem? TryParseRow(List<Word> row)
    {
        var tokens = row.Select(w => w.Text.Trim()).Where(t => t.Length > 0).ToList();
        if (tokens.Count < 3) return null;

        // Encontra o índice da data (pode não ser o token 0 — alguns bancos têm nº de lançamento antes)
        int dataIdx = -1;
        for (int i = 0; i < Math.Min(3, tokens.Count); i++)
        {
            if (DateRegex().IsMatch(tokens[i]) && TryParseData(tokens[i], out _))
            {
                dataIdx = i;
                break;
            }
        }
        if (dataIdx < 0) return null;
        if (!TryParseData(tokens[dataIdx], out var data)) return null;

        // Encontra o valor: último token que bate com o padrão de moeda BR
        int valorIdx = -1;
        for (int i = tokens.Count - 1; i > dataIdx; i--)
        {
            if (ValueRegex().IsMatch(tokens[i]))
            {
                valorIdx = i;
                break;
            }
        }
        if (valorIdx < 0) return null;

        if (!TryParseDecimalBR(tokens[valorIdx], out var valor)) return null;
        if (valor <= 0) return null; // ignora créditos/estornos

        // Descrição = tokens entre data e valor
        // Se houver colunas extras após o valor (pontos, categoria etc.) são ignoradas
        var descricao = string.Join(" ", tokens.Skip(dataIdx + 1).Take(valorIdx - dataIdx - 1)).Trim();
        if (string.IsNullOrWhiteSpace(descricao)) return null;

        if (descricao.Length > 100) descricao = descricao[..100];

        return new CsvFaturaItem(data, descricao, valor);
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private static readonly string[] DateFormats =
        ["dd/MM/yyyy", "dd/MM/yy", "d/M/yyyy", "d/M/yy"];

    private static bool TryParseData(string s, out DateOnly result)
    {
        result = default;
        foreach (var fmt in DateFormats)
        {
            if (DateOnly.TryParseExact(s, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
                return true;
        }
        return false;
    }

    private static bool TryParseDecimalBR(string s, out decimal result)
    {
        result = 0;
        s = s.Replace("R$", "").Trim();
        bool negative = s.StartsWith('-');
        if (negative) s = s[1..];
        if (decimal.TryParse(s, NumberStyles.Any, new CultureInfo("pt-BR"), out result))
        {
            if (negative) result = -result;
            return true;
        }
        return false;
    }
}
