using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using ControleFinanceiro.Application.ImportacoesWhatsapp;
using ControleFinanceiro.Domain.ImportacoesWhatsapp;

namespace ControleFinanceiro.Infrastructure.ImportacoesWhatsapp;

public sealed partial class HeuristicImportSuggestionService : IImportSuggestionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<IReadOnlyCollection<ImportSuggestionItem>> GenerateAsync(
        ImportSuggestionRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedText = request.TextoExtraido.Trim();
        var invoiceItems = TryBuildCardInvoiceItems(request, normalizedText);
        if (invoiceItems.Count > 0)
        {
            return Task.FromResult<IReadOnlyCollection<ImportSuggestionItem>>(invoiceItems);
        }

        var normalizedTextLower = normalizedText.ToLowerInvariant();

        var tipoSugestao =
            normalizedTextLower.Contains("cartao")
                ? TipoSugestaoImportacaoWhatsapp.CompraCartao
                : normalizedTextLower.Contains("extrato")
                    ? TipoSugestaoImportacaoWhatsapp.ItemExtrato
                    : normalizedTextLower.Contains("recebido") || normalizedTextLower.Contains("cliente") || normalizedTextLower.Contains("pix")
                        ? TipoSugestaoImportacaoWhatsapp.ContaReceber
                        : normalizedTextLower.Contains("transferencia") || normalizedTextLower.Contains("deposito")
                            ? TipoSugestaoImportacaoWhatsapp.Movimentacao
                            : TipoSugestaoImportacaoWhatsapp.ContaPagar;

        var payload = new
        {
            descricao = normalizedText.Length > 120 ? normalizedText[..120] : normalizedText,
            valor = ExtrairValor(normalizedText),
            dataIdentificada = ExtrairData(normalizedText),
            tipoMovimentacaoSugerido = normalizedTextLower.Contains("receb")
                || normalizedTextLower.Contains("deposito")
                || normalizedTextLower.Contains("credito")
                ? "Entrada"
                : normalizedTextLower.Contains("pag")
                    || normalizedTextLower.Contains("debito")
                    || normalizedTextLower.Contains("compra")
                    ? "Saida"
                    : null
        };

        IReadOnlyCollection<ImportSuggestionItem> items =
        [
            new ImportSuggestionItem(
                tipoSugestao,
                JsonSerializer.Serialize(payload, JsonOptions))
        ];

        return Task.FromResult(items);
    }

    private static IReadOnlyCollection<ImportSuggestionItem> TryBuildCardInvoiceItems(
        ImportSuggestionRequest request,
        string normalizedText)
    {
        if (!normalizedText.Contains("DOCUMENTO|FATURA_CARTAO", StringComparison.Ordinal))
        {
            return Array.Empty<ImportSuggestionItem>();
        }

        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var items = new List<ImportSuggestionItem>();

        foreach (var rawLine in normalizedText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!rawLine.StartsWith("ITEM|", StringComparison.Ordinal))
            {
                var metadataSeparator = rawLine.IndexOf('|');
                if (metadataSeparator > 0)
                {
                    metadata[rawLine[..metadataSeparator]] = rawLine[(metadataSeparator + 1)..];
                }

                continue;
            }

            var segments = rawLine.Split('|', StringSplitOptions.TrimEntries);
            if (segments.Length < 4)
            {
                continue;
            }

            var amount = ExtrairValor(segments[3]);
            var extras = ParseItemExtras(segments.Skip(4));
            var payload = new
            {
                descricao = segments[2],
                valor = amount,
                dataIdentificada = segments[1],
                tipoMovimentacaoSugerido = amount < 0 ? "Entrada" : "Saida",
                tipoDocumento = metadata.GetValueOrDefault("DOCUMENTO"),
                emissor = metadata.GetValueOrDefault("EMISSOR"),
                titular = metadata.GetValueOrDefault("TITULAR"),
                cartaoFinal = extras.GetValueOrDefault("CARTAO_FINAL") ?? metadata.GetValueOrDefault("CARTAO_FINAL"),
                portador = extras.GetValueOrDefault("PORTADOR"),
                parcela = extras.GetValueOrDefault("PARCELA"),
                dataVencimento = metadata.GetValueOrDefault("VENCIMENTO"),
                periodoInicio = metadata.GetValueOrDefault("PERIODO_INICIO"),
                periodoFim = metadata.GetValueOrDefault("PERIODO_FIM"),
                ehEstorno = string.Equals(extras.GetValueOrDefault("ESTORNO"), "true", StringComparison.OrdinalIgnoreCase),
                moedaOrigem = extras.GetValueOrDefault("MOEDA_ORIGEM"),
                valorMoedaOrigem = ExtrairValor(extras.GetValueOrDefault("VALOR_MOEDA_ORIGEM")),
                cotacao = ExtrairValor(extras.GetValueOrDefault("COTACAO"))
            };

            items.Add(new ImportSuggestionItem(
                TipoSugestaoImportacaoWhatsapp.CompraCartao,
                JsonSerializer.Serialize(payload, JsonOptions)));
        }

        return items;
    }

    private static Dictionary<string, string> ParseItemExtras(IEnumerable<string> segments)
    {
        var extras = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var segment in segments)
        {
            var separatorIndex = segment.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            extras[segment[..separatorIndex]] = segment[(separatorIndex + 1)..];
        }

        return extras;
    }

    private static decimal? ExtrairValor(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var match = ValorRegex().Match(text);
        if (!match.Success)
        {
            return null;
        }

        var rawValue = match.Value.Replace(".", string.Empty).Replace(',', '.');
        return decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? decimal.Round(value, 2, MidpointRounding.AwayFromZero)
            : null;
    }

    private static string? ExtrairData(string text)
    {
        var match = DataRegex().Match(text);
        return match.Success ? match.Value : null;
    }

    [GeneratedRegex(@"(?<!\d)-?(?:\d[\d\.]*,\d{2}|\d+\.\d{2})(?!\d)")]
    private static partial Regex ValorRegex();

    [GeneratedRegex(@"\b\d{4}-\d{2}-\d{2}\b")]
    private static partial Regex DataRegex();
}
