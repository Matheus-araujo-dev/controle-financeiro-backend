using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ControleFinanceiro.Infrastructure.ImportacoesWhatsapp;

internal static partial class CardInvoiceTextNormalizer
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly Dictionary<string, int> MonthMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["JAN"] = 1,
        ["FEV"] = 2,
        ["MAR"] = 3,
        ["ABR"] = 4,
        ["MAI"] = 5,
        ["JUN"] = 6,
        ["JUL"] = 7,
        ["AGO"] = 8,
        ["SET"] = 9,
        ["OUT"] = 10,
        ["NOV"] = 11,
        ["DEZ"] = 12
    };

    public static bool TryNormalize(IReadOnlyCollection<string> tokens, out string normalizedText)
    {
        var cleanedTokens = tokens
            .Select(CleanToken)
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .ToArray();

        var invoice = ParseBradesco(cleanedTokens) ?? ParseNubank(cleanedTokens);
        if (invoice is null)
        {
            normalizedText = string.Empty;
            return false;
        }

        normalizedText = BuildNormalizedText(invoice);
        return true;
    }

    public static bool IsInvoiceText(string value)
    {
        return value.Contains("DOCUMENTO|FATURA_CARTAO", StringComparison.Ordinal);
    }

    private static CardInvoiceDocument? ParseBradesco(string[] tokens)
    {
        if (!tokens.Any(token => NormalizeForLookup(token).Contains("BRADESCO")))
        {
            return null;
        }

        var issueDate = tokens
            .Select(TryParseBradescoIssueDate)
            .FirstOrDefault(date => date is not null);
        var dueDate = TryFindBradescoDueDate(tokens, issueDate);
        var holder = tokens.FirstOrDefault(token =>
                token.Contains(" - ", StringComparison.Ordinal) &&
                !NormalizeForLookup(token).StartsWith("DATA:", StringComparison.Ordinal))
            ?.Split(" - ", 2, StringSplitOptions.TrimEntries)[0];
        var cardEnding = tokens
            .Select(TryParseCardEnding)
            .FirstOrDefault(value => value is not null);
        var status = tokens
            .FirstOrDefault(token => NormalizeForLookup(token).StartsWith("SITUACAO DO EXTRATO"))
            ?.Split(':', 2, StringSplitOptions.TrimEntries)
            .LastOrDefault();

        var items = new List<CardInvoiceItem>();

        for (var index = 0; index < tokens.Length - 2; index++)
        {
            if (issueDate is null || !TryParseSlashDate(tokens[index], issueDate.Value.Year, issueDate.Value.Month, out var transactionDate))
            {
                continue;
            }

            var description = tokens[index + 1];
            if (IsIgnoredBradescoDescription(description))
            {
                continue;
            }

            if (index + 6 < tokens.Length && tokens[index + 2].Equals("USD", StringComparison.OrdinalIgnoreCase))
            {
                var currency = tokens[index + 2].ToUpperInvariant();
                var originalAmount = TryParseAmount(tokens[index + 3]) ?? TryParseAmount(tokens[index + 4]);
                decimal? exchangeRate = null;
                decimal? brlAmount = null;
                var nextIndex = index + 3;

                for (var scan = index + 3; scan < Math.Min(index + 8, tokens.Length - 1); scan++)
                {
                    if (!tokens[scan].StartsWith("R$", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    exchangeRate = TryParseAmount(tokens[scan]);
                    brlAmount = TryParseAmount(tokens[scan + 1]);
                    nextIndex = scan + 1;
                    break;
                }

                if (brlAmount is not null)
                {
                    items.Add(CreateItem(
                        transactionDate,
                        description,
                        brlAmount.Value,
                        holder,
                        cardEnding,
                        currency,
                        originalAmount,
                        exchangeRate));
                    index = nextIndex;
                    continue;
                }
            }

            // Scan ahead: after the description there may be an optional holder name
            // and/or an installment marker (e.g. "MATHEUS" then "4/4") before the amount.
            // We look up to 4 tokens beyond the description position to find the value.
            string? itemHolder = null;
            string? itemInstallment = null;
            decimal? amount = null;
            var amountIndex = index + 2;

            for (var lookahead = index + 2; lookahead <= Math.Min(index + 5, tokens.Length - 1); lookahead++)
            {
                var candidate = tokens[lookahead];
                var parsed = TryParseAmount(candidate);
                if (parsed is not null)
                {
                    amount = parsed;
                    amountIndex = lookahead;
                    break;
                }

                // Installment marker like "4/4" — capture it but keep looking
                if (InstallmentRegex().IsMatch(candidate) && candidate.Length <= 7)
                {
                    itemInstallment = candidate;
                    continue;
                }

                // Treat as holder name if no amount found yet and it looks like a proper name
                if (itemHolder is null && !IsDateToken(candidate) && TryParseCardEnding(candidate) is null)
                {
                    itemHolder = candidate;
                }
            }

            if (amount is null)
            {
                continue;
            }

            var itemDescription = AppendInstallmentSuffix(description, itemInstallment);

            items.Add(new CardInvoiceItem(
                transactionDate,
                itemDescription,
                amount.Value,
                itemHolder ?? holder,
                cardEnding,
                itemInstallment ?? ExtractInstallment(itemDescription),
                null,
                null,
                null,
                amount.Value < 0 || NormalizeForLookup(itemDescription).Contains("ESTORNO")));
            index = amountIndex;
        }

        return new CardInvoiceDocument("BRADESCO", holder, cardEnding, status, dueDate, null, null, null, items);
    }

    private static CardInvoiceDocument? ParseNubank(string[] tokens)
    {
        if (!LooksLikeNubank(tokens))
        {
            return null;
        }

        var dueDate = TryReadDateAfterLabel(tokens, "DATA DE VENCIMENTO");
        var periodText = TryReadValueAfterLabel(tokens, "PERIODO VIGENTE");
        TryParsePeriodRange(periodText, dueDate?.Year ?? DateTime.UtcNow.Year, dueDate?.Month ?? DateTime.UtcNow.Month, out var periodStart, out var periodEnd);

        var total = TryParseAmountAfterLabel(tokens, "TOTAL A PAGAR");
        var holder = FindHolderBeforeLabel(tokens, "FATURA");
        var items = new List<CardInvoiceItem>();

        string? currentHolder = null;
        var transactionSectionStarted = false;

        for (var index = 0; index < tokens.Length; index++)
        {
            var currentToken = tokens[index];
            var normalizedToken = NormalizeForLookup(currentToken);

            if (normalizedToken is "TRANSACOES" or "PAGAMENTOS E FINANCIAMENTOS")
            {
                transactionSectionStarted = true;
                continue;
            }

            if (!transactionSectionStarted)
            {
                continue;
            }

            if (IsTerminalNubankSectionHeader(normalizedToken))
            {
                break;
            }

            if (normalizedToken.StartsWith("DE ") && normalizedToken.Contains(" A "))
            {
                continue;
            }

            if (TryParseNubankGroupHeader(tokens, index, out var detectedHolder))
            {
                currentHolder = detectedHolder;
                index++;
                continue;
            }

            if (dueDate is null || !TryParseDayMonthDate(currentToken, dueDate.Value.Year, dueDate.Value.Month, out var transactionDate))
            {
                continue;
            }

            var cursor = index + 1;
            string? cardEnding = null;

            if (cursor < tokens.Length && TryParseCardEnding(tokens[cursor]) is { } parsedCardEnding)
            {
                cardEnding = parsedCardEnding;
                cursor++;
            }

            while (cursor < tokens.Length && IsIgnorableNubankDecorativeToken(tokens[cursor]))
            {
                cursor++;
            }

            if (cursor >= tokens.Length)
            {
                continue;
            }

            var description = tokens[cursor];
            cursor++;

            if (cursor >= tokens.Length)
            {
                continue;
            }

            decimal? amount = null;
            var amountIndex = cursor;

            for (var lookahead = cursor; lookahead <= Math.Min(cursor + 4, tokens.Length - 1); lookahead++)
            {
                var parsedAmount = TryParseAmount(tokens[lookahead]);
                if (parsedAmount is null)
                {
                    continue;
                }

                amount = parsedAmount;
                amountIndex = lookahead;
                break;
            }

            if (amount is null)
            {
                continue;
            }

            if (IsIgnoredNubankDescription(description))
            {
                index = amountIndex;
                continue;
            }

            items.Add(CreateItem(transactionDate, description, amount.Value, currentHolder, cardEnding));
            index = amountIndex;
        }

        return new CardInvoiceDocument("NUBANK", holder, null, null, dueDate, periodStart, periodEnd, total, items);
    }

    private static bool LooksLikeNubank(string[] tokens)
    {
        return tokens.Any(token => NormalizeForLookup(token).Contains("NUBANK")) ||
               (tokens.Any(token => NormalizeForLookup(token) == "RESUMO DA FATURA ATUAL") &&
                tokens.Any(token => NormalizeForLookup(token) == "TRANSACOES"));
    }

    private static string BuildNormalizedText(CardInvoiceDocument document)
    {
        var builder = new StringBuilder();
        builder.AppendLine("DOCUMENTO|FATURA_CARTAO");
        builder.AppendLine($"EMISSOR|{document.Emissor}");

        if (!string.IsNullOrWhiteSpace(document.Titular))
        {
            builder.AppendLine($"TITULAR|{SanitizeValue(document.Titular)}");
        }

        if (!string.IsNullOrWhiteSpace(document.CartaoFinal))
        {
            builder.AppendLine($"CARTAO_FINAL|{document.CartaoFinal}");
        }

        if (!string.IsNullOrWhiteSpace(document.Situacao))
        {
            builder.AppendLine($"SITUACAO|{SanitizeValue(document.Situacao)}");
        }

        if (document.Vencimento is not null)
        {
            builder.AppendLine($"VENCIMENTO|{document.Vencimento:yyyy-MM-dd}");
        }

        if (document.PeriodoInicio is not null)
        {
            builder.AppendLine($"PERIODO_INICIO|{document.PeriodoInicio:yyyy-MM-dd}");
        }

        if (document.PeriodoFim is not null)
        {
            builder.AppendLine($"PERIODO_FIM|{document.PeriodoFim:yyyy-MM-dd}");
        }

        if (document.Total is not null)
        {
            builder.AppendLine($"TOTAL|{FormatAmount(document.Total.Value)}");
        }

        foreach (var item in document.Itens)
        {
            builder.Append("ITEM|")
                .Append(item.Data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                .Append('|')
                .Append(SanitizeValue(item.Descricao))
                .Append('|')
                .Append(FormatAmount(item.Valor));

            if (!string.IsNullOrWhiteSpace(item.Portador))
            {
                builder.Append("|PORTADOR=").Append(SanitizeValue(item.Portador));
            }

            if (!string.IsNullOrWhiteSpace(item.CartaoFinal))
            {
                builder.Append("|CARTAO_FINAL=").Append(item.CartaoFinal);
            }

            if (!string.IsNullOrWhiteSpace(item.Parcela))
            {
                builder.Append("|PARCELA=").Append(item.Parcela);
            }

            if (!string.IsNullOrWhiteSpace(item.MoedaOrigem))
            {
                builder.Append("|MOEDA_ORIGEM=").Append(item.MoedaOrigem);
            }

            if (item.ValorMoedaOrigem is not null)
            {
                builder.Append("|VALOR_MOEDA_ORIGEM=").Append(FormatAmount(item.ValorMoedaOrigem.Value));
            }

            if (item.Cotacao is not null)
            {
                builder.Append("|COTACAO=").Append(FormatAmount(item.Cotacao.Value));
            }

            if (item.EhEstorno)
            {
                builder.Append("|ESTORNO=true");
            }

            builder.AppendLine();
        }

        return builder.ToString().Trim();
    }

    private static CardInvoiceItem CreateItem(
        DateOnly transactionDate,
        string description,
        decimal amount,
        string? holder,
        string? cardEnding,
        string? currency = null,
        decimal? originalAmount = null,
        decimal? exchangeRate = null)
    {
        return new CardInvoiceItem(
            transactionDate,
            description,
            amount,
            holder,
            cardEnding,
            ExtractInstallment(description),
            currency,
            originalAmount,
            exchangeRate,
            amount < 0 || NormalizeForLookup(description).Contains("ESTORNO"));
    }

    private static string? TryReadValueAfterLabel(string[] tokens, string normalizedLabel)
    {
        for (var index = 0; index < tokens.Length - 1; index++)
        {
            if (NormalizeForLookup(tokens[index]).StartsWith(normalizedLabel, StringComparison.Ordinal))
            {
                return tokens[index + 1];
            }
        }

        return null;
    }

    private static DateOnly? TryReadDateAfterLabel(string[] tokens, string normalizedLabel)
    {
        var value = TryReadValueAfterLabel(tokens, normalizedLabel);
        return value is null ? null : TryParseMonthDateWithYear(value);
    }

    private static decimal? TryParseAmountAfterLabel(string[] tokens, string normalizedLabel)
    {
        var value = TryReadValueAfterLabel(tokens, normalizedLabel);
        return value is null ? null : TryParseAmount(value);
    }

    private static string? FindHolderBeforeLabel(string[] tokens, string normalizedLabel)
    {
        for (var index = 1; index < tokens.Length; index++)
        {
            if (NormalizeForLookup(tokens[index]) != normalizedLabel)
            {
                continue;
            }

            var candidate = tokens[index - 1];
            if (NormalizeForLookup(candidate) is "FATURA" or "TRANSACOES")
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    private static bool TryParseNubankGroupHeader(string[] tokens, int index, out string? holder)
    {
        holder = null;
        if (index >= tokens.Length - 1)
        {
            return false;
        }

        var current = tokens[index];
        var normalizedCurrent = NormalizeForLookup(current);
        if (normalizedCurrent.Length == 0 ||
            IsDateToken(current) ||
            TryParseAmount(current) is not null ||
            normalizedCurrent is "FATURA" or "TRANSACOES" or "EMISSAO E ENVIO" or "RESUMO DA FATURA ATUAL")
        {
            return false;
        }

        if (TryParseAmount(tokens[index + 1]) is null)
        {
            return false;
        }

        holder = normalizedCurrent.StartsWith("COMPRAS DE ", StringComparison.Ordinal)
            ? current["Compras de ".Length..]
            : current;

        return true;
    }

    private static bool IsIgnorableNubankDecorativeToken(string value)
    {
        var normalized = NormalizeForLookup(value);
        return normalized.Length > 0 && !normalized.Any(char.IsLetterOrDigit);
    }

    private static bool IsTerminalNubankSectionHeader(string normalizedValue)
    {
        return normalizedValue is "PROXIMAS FATURAS" or "LIMITES DISPONIVEIS";
    }

    private static bool IsIgnoredBradescoDescription(string description)
    {
        var normalized = NormalizeForLookup(description);
        return normalized is "PAGTO. POR DEB EM C/C" or "SALDO ANTERIOR";
    }

    private static bool IsIgnoredNubankDescription(string description)
    {
        var normalized = NormalizeForLookup(description);
        return normalized.StartsWith("PAGAMENTO EM ", StringComparison.Ordinal) ||
               normalized is "EMISSAO E ENVIO";
    }

    private static string CleanToken(string token)
    {
        return token.Replace('−', '-').Replace('•', '*');
    }

    private static string NormalizeForLookup(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (char.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.ToUpperInvariant(character));
        }

        return Regex.Replace(builder.ToString(), "\\s+", " ").Trim();
    }

    private static string SanitizeValue(string value)
    {
        return value.Replace('|', '/').Trim();
    }

    private static string? TryParseCardEnding(string value)
    {
        var match = CardEndingRegex().Match(value);
        return match.Success ? match.Groups["digits"].Value : null;
    }

    private static bool IsDateToken(string value)
    {
        return SlashDateRegex().IsMatch(value) || DayMonthRegex().IsMatch(NormalizeForLookup(value));
    }

    private static DateOnly? TryParseBradescoIssueDate(string value)
    {
        var match = FullSlashDateRegex().Match(value);
        return !match.Success
            ? null
            : new DateOnly(
                int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture),
                int.Parse(match.Groups["month"].Value, CultureInfo.InvariantCulture),
                int.Parse(match.Groups["day"].Value, CultureInfo.InvariantCulture));
    }

    private static DateOnly? TryFindBradescoDueDate(string[] tokens, DateOnly? issueDate)
    {
        var referenceDate = issueDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        for (var index = 0; index < tokens.Length; index++)
        {
            var token = tokens[index];
            var normalizedToken = NormalizeForLookup(token);
            if (!normalizedToken.Contains("VENCIMENTO", StringComparison.Ordinal))
            {
                continue;
            }

            if (TryParseBradescoDueDateToken(token, referenceDate) is { } parsedFromToken)
            {
                return parsedFromToken;
            }

            for (var offset = 1; offset <= 2 && index + offset < tokens.Length; offset++)
            {
                if (TryParseBradescoDueDateToken(tokens[index + offset], referenceDate) is { } parsedFromNextToken)
                {
                    return parsedFromNextToken;
                }
            }
        }

        return null;
    }

    private static DateOnly? TryParseBradescoDueDateToken(string value, DateOnly referenceDate)
    {
        var fullMatch = FullSlashDateRegex().Match(value);
        if (fullMatch.Success)
        {
            return new DateOnly(
                int.Parse(fullMatch.Groups["year"].Value, CultureInfo.InvariantCulture),
                int.Parse(fullMatch.Groups["month"].Value, CultureInfo.InvariantCulture),
                int.Parse(fullMatch.Groups["day"].Value, CultureInfo.InvariantCulture));
        }

        var shortMatch = SlashDateRegex().Match(value);
        if (!shortMatch.Success)
        {
            return null;
        }

        var day = int.Parse(shortMatch.Groups["day"].Value, CultureInfo.InvariantCulture);
        var month = int.Parse(shortMatch.Groups["month"].Value, CultureInfo.InvariantCulture);
        var year = month < referenceDate.Month ? referenceDate.Year + 1 : referenceDate.Year;
        return new DateOnly(year, month, day);
    }

    private static DateOnly? TryParseMonthDateWithYear(string value)
    {
        var match = MonthDateWithYearRegex().Match(NormalizeForLookup(value));
        if (!match.Success || !MonthMap.TryGetValue(match.Groups["month"].Value, out var month))
        {
            return null;
        }

        return new DateOnly(
            int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture),
            month,
            int.Parse(match.Groups["day"].Value, CultureInfo.InvariantCulture));
    }

    private static bool TryParsePeriodRange(string? value, int referenceYear, int referenceMonth, out DateOnly? start, out DateOnly? end)
    {
        start = null;
        end = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var match = PeriodRangeRegex().Match(NormalizeForLookup(value));
        if (!match.Success)
        {
            return false;
        }

        if (!MonthMap.TryGetValue(match.Groups["startMonth"].Value, out var startMonth) ||
            !MonthMap.TryGetValue(match.Groups["endMonth"].Value, out var endMonth))
        {
            return false;
        }

        start = new DateOnly(InferYear(startMonth, referenceYear, referenceMonth), startMonth, int.Parse(match.Groups["startDay"].Value, CultureInfo.InvariantCulture));
        end = new DateOnly(InferYear(endMonth, referenceYear, referenceMonth), endMonth, int.Parse(match.Groups["endDay"].Value, CultureInfo.InvariantCulture));
        return true;
    }

    private static bool TryParseSlashDate(string value, int referenceYear, int referenceMonth, out DateOnly date)
    {
        var match = SlashDateRegex().Match(value);
        if (!match.Success)
        {
            date = default;
            return false;
        }

        var month = int.Parse(match.Groups["month"].Value, CultureInfo.InvariantCulture);
        date = new DateOnly(InferYear(month, referenceYear, referenceMonth), month, int.Parse(match.Groups["day"].Value, CultureInfo.InvariantCulture));
        return true;
    }

    private static bool TryParseDayMonthDate(string value, int referenceYear, int referenceMonth, out DateOnly date)
    {
        var match = DayMonthRegex().Match(NormalizeForLookup(value));
        if (!match.Success || !MonthMap.TryGetValue(match.Groups["month"].Value, out var month))
        {
            date = default;
            return false;
        }

        date = new DateOnly(InferYear(month, referenceYear, referenceMonth), month, int.Parse(match.Groups["day"].Value, CultureInfo.InvariantCulture));
        return true;
    }

    private static int InferYear(int month, int referenceYear, int referenceMonth)
    {
        return month > referenceMonth ? referenceYear - 1 : referenceYear;
    }

    private static string? ExtractInstallment(string description)
    {
        var match = InstallmentRegex().Match(description);
        return match.Success ? match.Groups["value"].Value : null;
    }

    private static string AppendInstallmentSuffix(string description, string? installment)
    {
        if (string.IsNullOrWhiteSpace(installment) ||
            description.Contains(installment, StringComparison.OrdinalIgnoreCase))
        {
            return description;
        }

        return $"{description} {installment}";
    }

    private static decimal? TryParseAmount(string value)
    {
        if (InstallmentRegex().IsMatch(value) && !value.Contains(',') && !value.Contains('.'))
        {
            return null;
        }

        var normalized = value
            .Replace("R$", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("US$", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("−", "-")
            .Trim();

        if (normalized.StartsWith('(') && normalized.EndsWith(')'))
        {
            normalized = $"-{normalized[1..^1].Trim()}";
        }

        normalized = Regex.Replace(normalized, "\\s+", string.Empty);
        if (!AmountTokenRegex().IsMatch(normalized))
        {
            return null;
        }

        normalized = normalized.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.');

        return decimal.TryParse(normalized, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var amount)
            ? decimal.Round(amount, 2, MidpointRounding.AwayFromZero)
            : null;
    }

    private static string FormatAmount(decimal value)
    {
        return value.ToString("0.00", PtBr);
    }

    [GeneratedRegex(@"(?<day>\d{2})/(?<month>\d{2})/(?<year>\d{4})")]
    private static partial Regex FullSlashDateRegex();

    [GeneratedRegex(@"^(?<day>\d{2})/(?<month>\d{2})$")]
    private static partial Regex SlashDateRegex();

    [GeneratedRegex(@"^(?<day>\d{2}) (?<month>[A-Z]{3})(?: (?<year>\d{4}))?$")]
    private static partial Regex DayMonthRegex();

    [GeneratedRegex(@"^(?<day>\d{2}) (?<month>[A-Z]{3}) (?<year>\d{4})$")]
    private static partial Regex MonthDateWithYearRegex();

    [GeneratedRegex(@"(?<startDay>\d{2}) (?<startMonth>[A-Z]{3}) A (?<endDay>\d{2}) (?<endMonth>[A-Z]{3})")]
    private static partial Regex PeriodRangeRegex();

    [GeneratedRegex(@"(?<digits>\d{4})$")]
    private static partial Regex CardEndingRegex();

    [GeneratedRegex(@"(?<value>\d{1,2}/\d{1,2})")]
    private static partial Regex InstallmentRegex();

    [GeneratedRegex(@"^-?(?:\d[\d\.]*,\d{2}|\d+\.\d{2})$")]
    private static partial Regex AmountTokenRegex();

    private sealed record CardInvoiceDocument(
        string Emissor,
        string? Titular,
        string? CartaoFinal,
        string? Situacao,
        DateOnly? Vencimento,
        DateOnly? PeriodoInicio,
        DateOnly? PeriodoFim,
        decimal? Total,
        IReadOnlyCollection<CardInvoiceItem> Itens);

    private sealed record CardInvoiceItem(
        DateOnly Data,
        string Descricao,
        decimal Valor,
        string? Portador,
        string? CartaoFinal,
        string? Parcela,
        string? MoedaOrigem,
        decimal? ValorMoedaOrigem,
        decimal? Cotacao,
        bool EhEstorno);
}
