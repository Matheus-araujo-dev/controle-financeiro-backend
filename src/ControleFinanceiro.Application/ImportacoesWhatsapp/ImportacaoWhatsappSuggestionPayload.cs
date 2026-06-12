using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ControleFinanceiro.Application.ImportacoesWhatsapp;

public sealed partial record ImportacaoWhatsappSuggestionPayload(
    string? Descricao,
    decimal? Valor,
    DateOnly? DataIdentificada,
    DateOnly? DataVencimento,
    DateOnly? PeriodoInicio,
    DateOnly? PeriodoFim,
    string? TipoMovimentacaoSugerido,
    string? Emissor,
    string? CartaoFinal,
    string? Portador,
    string? Parcela)
{
    public static ImportacaoWhatsappSuggestionPayload Parse(string payloadSugeridoJson)
    {
        if (string.IsNullOrWhiteSpace(payloadSugeridoJson))
        {
            return new ImportacaoWhatsappSuggestionPayload(null, null, null, null, null, null, null, null, null, null, null);
        }

        using var document = JsonDocument.Parse(payloadSugeridoJson);
        var root = document.RootElement;

        return new ImportacaoWhatsappSuggestionPayload(
            GetString(root, "descricao"),
            GetDecimal(root, "valor"),
            GetDate(root, "dataIdentificada"),
            GetDate(root, "dataVencimento"),
            GetDate(root, "periodoInicio"),
            GetDate(root, "periodoFim"),
            GetString(root, "tipoMovimentacaoSugerido"),
            GetString(root, "emissor"),
            GetString(root, "cartaoFinal"),
            GetString(root, "portador"),
            GetString(root, "parcela"));
    }

    public string? BuildLearningKey()
    {
        return NormalizeKey([Descricao]);
    }

    public ParcelamentoCompraCartaoInfo? GetParcelamentoCompraCartaoInfo()
    {
        var source = !string.IsNullOrWhiteSpace(Parcela) ? Parcela : Descricao;
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        var match = ParcelaRegex().Match(source);
        if (!match.Success)
        {
            return null;
        }

        var numeroParcela = int.Parse(match.Groups["atual"].Value, CultureInfo.InvariantCulture);
        var quantidadeParcelas = int.Parse(match.Groups["total"].Value, CultureInfo.InvariantCulture);

        if (numeroParcela < 1 || quantidadeParcelas < 2 || numeroParcela > quantidadeParcelas)
        {
            return null;
        }

        return new ParcelamentoCompraCartaoInfo(numeroParcela, quantidadeParcelas);
    }

    public string? BuildInstallmentSeriesKey()
    {
        var parcelamento = GetParcelamentoCompraCartaoInfo();
        if (parcelamento is null || !Valor.HasValue)
        {
            return null;
        }

        var descricaoBase = StripInstallmentMarker(Descricao);
        return NormalizeKey(
        [
            Emissor,
            CartaoFinal,
            Portador,
            descricaoBase,
            Valor.Value.ToString("0.00", CultureInfo.InvariantCulture),
            parcelamento.QuantidadeParcelas.ToString(CultureInfo.InvariantCulture)
        ]);
    }

    public string? BuildRecurringSeriesKey()
    {
        if (!Valor.HasValue)
        {
            return null;
        }

        var descricaoBase = StripInstallmentMarker(Descricao);
        return NormalizeKey(
        [
            "RECORRENTE",
            Emissor,
            CartaoFinal,
            Portador,
            descricaoBase,
            Valor.Value.ToString("0.00", CultureInfo.InvariantCulture)
        ]);
    }

    public DateOnly? GetProjectedExpenseDate(int monthOffset)
    {
        return DataIdentificada?.AddMonths(monthOffset);
    }

    public DateOnly? GetProjectedInvoiceDueDate(int monthOffset)
    {
        var baseDate = DataVencimento ?? DataIdentificada;
        return baseDate?.AddMonths(monthOffset);
    }

    public static string AtualizarMarcadorParcela(
        string descricao,
        int numeroParcela,
        int quantidadeParcelas)
    {
        if (string.IsNullOrWhiteSpace(descricao))
        {
            return descricao;
        }

        return ParcelaRegex().Replace(descricao, $"{numeroParcela}/{quantidadeParcelas}", 1);
    }

    private static string? GetString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static decimal? GetDecimal(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetDecimal(out var decimalValue) => decimalValue,
            JsonValueKind.String when decimal.TryParse(property.GetString(), out var stringValue) => stringValue,
            _ => null
        };
    }

    private static DateOnly? GetDate(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return DateOnly.TryParse(property.GetString(), out var value) ? value : null;
    }

    private static string? NormalizeKey(IEnumerable<string?> parts)
    {
        var normalized = new string(
            string.Concat(parts.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part!.Trim().ToUpperInvariant()))
                .Where(char.IsLetterOrDigit)
                .ToArray());

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length <= 180 ? normalized : normalized[..180];
    }

    private static string? StripInstallmentMarker(string? descricao)
    {
        if (string.IsNullOrWhiteSpace(descricao))
        {
            return null;
        }

        var semParcela = ParcelaRegex().Replace(descricao, string.Empty);
        var compactada = string.Join(' ', semParcela.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return string.IsNullOrWhiteSpace(compactada) ? descricao.Trim() : compactada;
    }

    [GeneratedRegex(@"(?<!\d)(?<atual>\d{1,2})\s*/\s*(?<total>\d{1,2})(?!\d)")]
    private static partial Regex ParcelaRegex();
}

public sealed record ParcelamentoCompraCartaoInfo(int NumeroParcela, int QuantidadeParcelas);
