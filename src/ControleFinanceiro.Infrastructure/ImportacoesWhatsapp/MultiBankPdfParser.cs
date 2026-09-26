using System.Globalization;
using System.Text.RegularExpressions;
using ControleFinanceiro.Application.Financeiro.Importacao;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace ControleFinanceiro.Infrastructure.ImportacoesWhatsapp;

internal static class MultiBankPdfParser
{
    private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly string[] Months = ["JAN","FEV","MAR","ABR","MAI","JUN","JUL","AGO","SET","OUT","NOV","DEZ"];
    private static Match Find(string text, string pattern) => Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static string Normalize(string text) => Regex.Replace(text.Replace('−','-'), @"\s+", " ").Trim();
    private static decimal Amount(string text) => decimal.Parse(text, NumberStyles.Number, Pt);
    private static CsvFaturaParser.ParseResult Failure(string bank, string reason) => new([], $"{bank}: {reason} Nenhum lançamento foi criado.");

    public static CsvFaturaParser.ParseResult? Parse(PdfDocument document)
        => ParseRows(document.GetPages().Select(p => Rows(p.GetWords())).ToArray());

    internal static CsvFaturaParser.ParseResult? ParseRows(string[][] source)
    {
        var pages = source.Select(p => p.Select(Normalize).Where(s => s.Length > 0).ToArray()).ToArray();
        var all = string.Join("\n", pages.SelectMany(p => p));
        var bank = all.Contains("RESUMO DA FATURA ATUAL", StringComparison.OrdinalIgnoreCase) && (all.Contains("Nubank", StringComparison.OrdinalIgnoreCase) || all.Contains("Nu Pagamentos", StringComparison.OrdinalIgnoreCase)) ? "Nubank"
            : all.Contains("Mercado Pago", StringComparison.OrdinalIgnoreCase) && all.Contains("Detalhes de consumo", StringComparison.OrdinalIgnoreCase) ? "Mercado Pago"
            : all.Contains("Bmg", StringComparison.OrdinalIgnoreCase) && all.Contains("Lançamentos:", StringComparison.OrdinalIgnoreCase) ? "BMG" : null;
        if (bank is null) return null;
        try { return ParseKnown(pages, all, bank); }
        catch (Exception ex) when (ex is FormatException or ArgumentOutOfRangeException or OverflowException)
        { return Failure(bank, "Data, parcela ou valor inválido no PDF."); }
    }

    private static CsvFaturaParser.ParseResult ParseKnown(string[][] pages, string all, string bank)
    {
        DateOnly due;
        if (bank == "Nubank")
        {
            var date = Find(all, @"(?:FATURA|Data de vencimento:)\s+(\d{2})\s+([A-Z]{3})\s+(\d{4})");
            if (!date.Success) return Failure(bank,"Vencimento não encontrado.");
            due = new(int.Parse(date.Groups[3].Value), Array.IndexOf(Months, date.Groups[2].Value.ToUpperInvariant()) + 1, int.Parse(date.Groups[1].Value));
        }
        else
        {
            var date = Find(all, @"Vencimento:[\s\S]{0,200}?(\d{2}/\d{2}/\d{4})");
            if (!DateOnly.TryParseExact(date.Groups[1].Value, "dd/MM/yyyy", Pt, DateTimeStyles.None, out due)) return Failure(bank,"Vencimento não encontrado.");
        }
        var totalMatch = bank switch
        {
            "Nubank" => Find(all[all.IndexOf("RESUMO DA FATURA ATUAL", StringComparison.OrdinalIgnoreCase)..], @"Total a pagar\s+R\$\s*([\d.]+,\d{2})"),
            "BMG" => Find(all, @"VALOR TOTAL DA FATURA\s+([\d.]+,\d{2})"),
            _ => Find(all, @"Total a pagar Vence em[^\n]*\nR\$\s*([\d.]+,\d{2})")
        };
        if (!totalMatch.Success && bank == "Mercado Pago")
            totalMatch = Find(string.Join("\n", pages.Where(p => p.Any(l => l.Contains("Detalhes de consumo", StringComparison.OrdinalIgnoreCase))).SelectMany(p => p)), @"(?:^|\n)Total R\$\s*([\d.]+,\d{2})");
        if (!totalMatch.Success) return Failure(bank,"Total da fatura não encontrado.");
        var total = Amount(totalMatch.Groups[1].Value);
        var previousMatch = Find(all, bank == "BMG" ? @"Saldo da fatura anterior\s*\+?\s*R\$\s*([\d.]+,\d{2})" : @"Fatura anterior\s+R\$\s*([\d.]+,\d{2})");
        var previous = previousMatch.Success ? Amount(previousMatch.Groups[1].Value) : 0;
        decimal payments = 0;
        var items = new List<CsvFaturaItem>();
        var marker = bank == "Nubank" ? "TRANSAÇÕES" : bank == "BMG" ? "Lançamentos:" : "Detalhes de consumo";
        foreach (var page in pages.Where(p => p.Any(l => l.StartsWith(marker, StringComparison.OrdinalIgnoreCase))))
        {
            var started = false;
            foreach (var line in page)
            {
                if (line.StartsWith(marker, StringComparison.OrdinalIgnoreCase)) { started = true; continue; }
                if (!started) continue;
                if (bank == "BMG" && line.StartsWith("VALOR TOTAL DA FATURA", StringComparison.OrdinalIgnoreCase)) break;
                if (bank == "Mercado Pago" && line.StartsWith("Total R$", StringComparison.OrdinalIgnoreCase)) break;
                if (!Find(line, @"^\d{2}(?:/\d{2}|\s+[A-Z]{3})\b").Success) continue;
                var row = Find(line, @"^(?<day>\d{2})(?:/(?<month>\d{2})|\s+(?<monthname>[A-Z]{3}))\s+(?<description>.+?)\s+(?<minus>-?)\s*(?:R\$\s*)?(?<value>\d[\d.]*,\d{2})$");
                if (!row.Success) return Failure(bank,"Lançamento sem valor ou descrição legível.");
                var value = Amount(row.Groups["value"].Value) * (row.Groups["minus"].Value == "-" ? -1 : 1);
                var description = Regex.Replace(row.Groups["description"].Value, @"^[•●*]{4}\s*\d{4}\s*", "");
                if (description.StartsWith("Pagamento em", StringComparison.OrdinalIgnoreCase) || description.StartsWith("Pgto Fatura", StringComparison.OrdinalIgnoreCase)) { payments += value; continue; }
                var installment = Find(description, bank switch {
                    "Nubank" => @"\bParcela\s+(\d+)/(\d+)\s*$",
                    "BMG" => @"\bPc\.\s*(\d+)/(\d+)\s*$",
                    _ => @"\bParcela\s+(\d+)\s+de\s+(\d+)\s*$"
                });
                var number = installment.Success ? int.Parse(installment.Groups[1].Value) : 1;
                var count = installment.Success ? int.Parse(installment.Groups[2].Value) : 1;
                if (number < 1 || count < number || count > 120 || value == 0) return Failure(bank,"Parcela ou valor inválido.");
                var month = row.Groups["month"].Success ? int.Parse(row.Groups["month"].Value) : Array.IndexOf(Months,row.Groups["monthname"].Value.ToUpperInvariant()) + 1;
                // BMG informa a data da compra original; Nubank informa a data do lançamento neste ciclo.
                var reference = bank == "BMG" ? due.AddMonths(1-number) : due;
                var date = new DateOnly(reference.Year,month,int.Parse(row.Groups["day"].Value));
                if (date > reference) date = date.AddYears(-1);
                items.Add(new(date, description, value, due, number, count));
            }
        }
        if (items.Count == 0) return Failure(bank,"Nenhum lançamento encontrado.");
        var difference = items.Sum(i=>i.Valor) + previous + payments - total;
        if (Math.Abs(difference) > 0.05m) return Failure(bank,"A soma dos lançamentos não confere com o total da fatura.");
        var warning = $"{bank}: {items.Count} lançamentos lidos. Total impresso {total.ToString("C",Pt)}. Pagamentos e saldo anterior não geram contas. Confira os itens antes de confirmar.";
        if (difference != 0) warning += $" Atenção: diferença de {Math.Abs(difference).ToString("C",Pt)} entre as linhas e o total impresso. Os valores originais foram preservados; revise a diferença.";
        return new(items, warning, total);
    }

    private static string[] Rows(IEnumerable<Word> words)
    {
        var result = new List<string>(); var row = new List<Word>(); double y = double.MaxValue;
        foreach (var word in words.OrderByDescending(w=>w.BoundingBox.Bottom))
        {
            if (Math.Abs(word.BoundingBox.Bottom-y)>2)
            {
                if(row.Count>0) result.Add(string.Join(" ",row.OrderBy(w=>w.BoundingBox.Left).Select(w=>w.Text)));
                row=[]; y=word.BoundingBox.Bottom;
            }
            row.Add(word);
        }
        if(row.Count>0) result.Add(string.Join(" ",row.OrderBy(w=>w.BoundingBox.Left).Select(w=>w.Text)));
        return result.ToArray();
    }
}
