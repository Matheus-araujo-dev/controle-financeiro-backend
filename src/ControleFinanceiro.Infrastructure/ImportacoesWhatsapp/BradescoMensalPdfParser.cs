using System.Globalization;
using System.Text.RegularExpressions;
using ControleFinanceiro.Application.Financeiro.Importacao;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace ControleFinanceiro.Infrastructure.ImportacoesWhatsapp;

internal static class BradescoMensalPdfParser
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly Regex ShortDate = new(@"^\d{2}/\d{2}$", RegexOptions.CultureInvariant);
    private static readonly Regex Money = new(@"^-?\d[\d.]*,\d{2}-?$", RegexOptions.CultureInvariant);

    public static CsvFaturaParser.ParseResult? Parse(byte[] bytes)
    {
        using var pdf = PdfDocument.Open(bytes);
        var pages = pdf.GetPages().Select(p => Rows(p.GetWords().ToArray())).ToArray();
        var header = pages.SelectMany(p => p).FirstOrDefault(r => r.Any(w => w.Text == "Cidade") && r.Any(w => w.Text == "US$") && r.Any(w => w.Text == "Data"));
        if (header is null || !pages.SelectMany(p => p).SelectMany(r => r).Any(w => w.Text.Contains("bradesco", StringComparison.OrdinalIgnoreCase))) return null;
        var dueLabel = pages[0].SelectMany(r => r).FirstOrDefault(w => w.Text.Equals("Vencimento", StringComparison.OrdinalIgnoreCase));
        if (dueLabel is null) return Failure("Vencimento não encontrado.");
        var dueWord = pages[0].SelectMany(r => r).FirstOrDefault(w => w.BoundingBox.Left > dueLabel.BoundingBox.Left - 10
            && w.BoundingBox.Bottom < dueLabel.BoundingBox.Bottom && w.BoundingBox.Bottom > dueLabel.BoundingBox.Bottom - 30
            && Regex.IsMatch(w.Text, @"^\d{2}/\d{2}/\d{4}$"));
        if (dueWord is null || !DateOnly.TryParseExact(dueWord.Text, "dd/MM/yyyy", PtBr, DateTimeStyles.None, out var due)) return Failure("Vencimento inválido.");
        var totalWord = pages[0].SelectMany(r => r).FirstOrDefault(w => w.BoundingBox.Left > dueWord.BoundingBox.Left - 100
            && w.BoundingBox.Right < dueWord.BoundingBox.Left && Math.Abs(w.BoundingBox.Bottom - dueWord.BoundingBox.Bottom) < 3 && Money.IsMatch(w.Text));
        if (totalWord is null) return Failure("Total da fatura não encontrado.");
        var total = Amount(totalWord.Text);
        var previousRow = pages[0].FirstOrDefault(r => Text(r).Contains("Saldo anterior", StringComparison.OrdinalIgnoreCase));
        var previousWord = previousRow?.LastOrDefault(w => Money.IsMatch(w.Text));
        if (previousWord is null) return Failure("Saldo anterior não encontrado para conferência.");
        var previous = Amount(previousWord.Text);
        var dateX = header.Single(w => w.Text == "Data").BoundingBox.Left;
        var cityX = header.Single(w => w.Text == "Cidade").BoundingBox.Left;
        var moneyX = header.Last(w => w.Text == "R$").BoundingBox.Left;
        var result = new List<CsvFaturaItem>();
        decimal payments = 0;
        Pending? pending = null;
        bool started = false;
        string? error = null;

        void Flush()
        {
            if (pending is null) return;
            if (pending.Description.StartsWith("PAGTO", StringComparison.OrdinalIgnoreCase) || pending.Description.StartsWith("PAGAMENTO", StringComparison.OrdinalIgnoreCase))
                payments += pending.Value;
            else
            {
                var installment = Regex.Match(pending.Description, @"(?<number>\d{2})/(?<total>\d{2})$");
                int number = installment.Success ? int.Parse(installment.Groups["number"].Value) : 1;
                int count = installment.Success ? int.Parse(installment.Groups["total"].Value) : 1;
                if (number < 1 || count < number) error = "Parcela inválida.";
                else result.Add(new CsvFaturaItem(pending.Date, pending.Description, pending.Value, due, number, count));
            }
            pending = null;
        }

        foreach (var page in pages)
        {
            foreach (var row in page)
            {
                if (ReferenceEquals(row, header)) { started = true; continue; }
                if (!started) continue;
                if (row.Any(w => w.Text == "XXXX") || Text(row).StartsWith("Total", StringComparison.OrdinalIgnoreCase))
                { Flush(); continue; }
                var date = row.FirstOrDefault(w => Math.Abs(w.BoundingBox.Left - dateX) < 3 && ShortDate.IsMatch(w.Text));
                if (date is not null)
                {
                    Flush();
                    var candidates = row.Where(w => w.BoundingBox.Left >= moneyX - 35 && w.BoundingBox.Right < moneyX + 30 && Money.IsMatch(w.Text)).ToArray();
                    if (candidates.Length != 1) return Failure("Lançamento sem valor único na coluna R$.");
                    var description = Text(row.Where(w => w.BoundingBox.Left > dateX + 18 && w.BoundingBox.Left < cityX - 1));
                    description = Regex.Replace(description, @"\s+USD\b.*$", "");
                    description = Regex.Replace(description, @"\s+\d+,\d{2}\S*$", "");
                    if (string.IsNullOrWhiteSpace(description)) return Failure("Lançamento sem descrição.");
                    if (!DateOnly.TryParseExact(date.Text + "/" + due.Year, "dd/MM/yyyy", PtBr, DateTimeStyles.None, out var transaction)) return Failure("Data inválida.");
                    if (transaction > due) transaction = transaction.AddYears(-1);
                    var value = Amount(candidates[0].Text);
                    if (row.Any(w => (w.Text == "-" || w.Text == "−") && w.BoundingBox.Left >= candidates[0].BoundingBox.Right - 1
                        && w.BoundingBox.Left < candidates[0].BoundingBox.Right + 12)) value = -Math.Abs(value);
                    pending = new Pending(transaction, description, value, date.BoundingBox.Bottom);
                }
                else if (pending is not null)
                {
                    var continuation = row.Where(w => w.BoundingBox.Left > dateX + 18 && w.BoundingBox.Left < cityX - 1
                        && pending.Y - w.BoundingBox.Bottom > 1 && pending.Y - w.BoundingBox.Bottom < 10).ToArray();
                    if (continuation.Length > 0)
                    {
                        var text = Text(continuation);
                        var separator = Regex.IsMatch(pending.Description, @"\d{2}/\d$") && Regex.IsMatch(text, @"^\d$") ? "" : " ";
                        pending = pending with { Description = pending.Description + separator + text, Y = continuation[0].BoundingBox.Bottom };
                    }
                }
            }
            Flush();
        }
        if (error is not null) return Failure(error);
        if (result.Count == 0 || decimal.Round(result.Sum(i => i.Valor) + previous + payments, 2) != total)
            return Failure($"Conferência divergente: {result.Count} itens, soma {result.Sum(i => i.Valor):F2}, saldo anterior {previous:F2}, pagamentos {payments:F2}, total {total:F2}. Nenhum item liberado para importação.");
        return new(result, $"Bradesco: {result.Count} lançamentos conferidos com o total da fatura de {total.ToString("C", PtBr)}. Pagamentos e saldo anterior foram usados somente na conferência. Revise antes de confirmar.");
    }

    private static decimal Amount(string value) => decimal.Parse(value.TrimEnd('-'), NumberStyles.Number, PtBr) * (value.EndsWith('-') ? -1 : 1);
    private static string Text(IEnumerable<Word> words) => string.Join(" ", words.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text));
    private static CsvFaturaParser.ParseResult Failure(string reason) => new([], "Fatura Bradesco não importada: " + reason);
    private sealed record Pending(DateOnly Date, string Description, decimal Value, double Y);
    private static List<Word[]> Rows(Word[] words)
    {
        var rows = new List<Word[]>();
        var row = new List<Word>();
        double y = double.MaxValue;
        foreach (var word in words.OrderByDescending(w => w.BoundingBox.Bottom))
        {
            if (Math.Abs(word.BoundingBox.Bottom - y) > 2)
            {
                if (row.Count > 0) rows.Add(row.OrderBy(w => w.BoundingBox.Left).ToArray());
                row = []; y = word.BoundingBox.Bottom;
            }
            row.Add(word);
        }
        if (row.Count > 0) rows.Add(row.OrderBy(w => w.BoundingBox.Left).ToArray());
        return rows;
    }
}
