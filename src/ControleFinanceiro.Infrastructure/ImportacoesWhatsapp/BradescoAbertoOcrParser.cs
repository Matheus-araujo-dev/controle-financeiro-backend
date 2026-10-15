using System.Globalization;
using System.Text.RegularExpressions;
using ControleFinanceiro.Application.Financeiro.Importacao;

namespace ControleFinanceiro.Infrastructure.ImportacoesWhatsapp;

internal static class BradescoAbertoOcrParser
{
    private static Match Match(string value, string pattern) => Regex.Match(value, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static CsvFaturaParser.ParseResult Fail(string reason) => new([], "Extrato Bradesco não importado: " + reason + " Nenhum lançamento foi criado.");

    public static CsvFaturaParser.ParseResult Parse(string text, DateOnly? due)
    {
        if (due is null) return Fail("Abra a fatura desejada e use Conciliar PDF para informar o vencimento deste extrato aberto.");
        if (!text.Contains("Bradesco", StringComparison.OrdinalIgnoreCase) || !text.Contains("Em Aberto", StringComparison.OrdinalIgnoreCase))
            return Fail("O layout de extrato aberto não foi reconhecido.");
        var date = Match(text, @"Data:\s*(\d{2}/\d{2}/\d{4})");
        if (!DateOnly.TryParseExact(date.Groups[1].Value, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var issued))
            return Fail("Data de emissão ilegível.");
        var lines = text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var items = new List<CsvFaturaItem>();
        decimal subtotal = 0;
        var pending = false;
        var totals = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.StartsWith("Total para:", StringComparison.OrdinalIgnoreCase))
            {
                var total = Match(line, @"R\$\s*([\d.,/]+)\s*$");
                if (total.Success ? !Money(total.Groups[1].Value, out var amount) || amount != subtotal : subtotal != 0 || pending)
                    return Fail("Os lançamentos lidos não conferem com o subtotal do cartão. Revise a qualidade do arquivo.");
                subtotal = 0; pending = false; totals++;
                continue;
            }
            var row = Match(line, @"^(\d{2})/?(\d{2})[\s\""'º”—-]*(.*?)\s+USD\s+(.*)$");
            if (!row.Success)
            {
                if (line.Contains("USD", StringComparison.OrdinalIgnoreCase)) return Fail("Uma linha de compra ficou ilegível.");
                continue;
            }
            var tail = row.Groups[4].Value;
            // Créditos longos podem aparecer em duas linhas, após o último R$.
            if (!Match(tail, @"R\$.*R\$\s*-?[\d.,/]+\s*$").Success && i + 1 < lines.Length && Match(lines[i + 1], @"^-?[\d.,/]+$").Success)
                tail += " R$ " + lines[++i];
            var money = Match(tail, @"R\$.*R\$\s*(-?[\d.,/]+)\s*$");
            if (!money.Success || !Money(money.Groups[1].Value, out var value)) return Fail("Valor de compra ilegível.");
            var shortDate = row.Groups[1].Value + "/" + row.Groups[2].Value;
            if (!DateOnly.TryParseExact(shortDate + "/" + issued.Year, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var when))
                return Fail("Data de compra inválida.");
            if (when > issued) when = when.AddYears(-1);
            if (when > due.Value) return Fail("A compra é posterior ao vencimento da fatura selecionada.");
            var description = row.Groups[3].Value.Trim(' ', '—', '-', '"', '”');
            if (description.Length == 0) return Fail("Descrição de compra ilegível.");
            subtotal += value; pending = true;
            if (description.StartsWith("PAGTO", StringComparison.OrdinalIgnoreCase) || description.StartsWith("PAGAMENTO", StringComparison.OrdinalIgnoreCase) || description.StartsWith("SALDO ANTERIOR", StringComparison.OrdinalIgnoreCase)) continue;
            if (value == 0) return Fail("Valor de compra inválido.");
            // Não inferir parcelas a partir de nomes de estabelecimentos ou de compras repetidas.
            items.Add(new(when, description, value, due));
        }
        if (pending || totals == 0 || items.Count == 0) return Fail("O extrato está incompleto ou sem subtotais legíveis.");
        return new(items, "Extrato aberto lido por OCR. Confira datas, descrições e valores. O vencimento é o da fatura selecionada; pagamentos e saldo anterior não geram compras.");
    }

    private static bool Money(string text, out decimal value)
    {
        // OCR pode ler vírgula como barra ou omiti-la. Só aceitamos após conferir o subtotal completo.
        text = text.Replace('/', ',');
        if (!text.Contains(','))
        {
            if (!Match(text, @"^-?\d{3,}$").Success) { value = 0; return false; }
            text = text.Insert(text.Length - 2, ",");
        }
        return Match(text, @"^-?\d+(?:\.\d{3})*,\d{2}$").Success
            ? decimal.TryParse(text, NumberStyles.Number, CultureInfo.GetCultureInfo("pt-BR"), out value)
            : Invalid(out value);
    }

    private static bool Invalid(out decimal value) { value = 0; return false; }
}
