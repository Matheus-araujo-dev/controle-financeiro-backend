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
            remetente = request.Remetente,
            tipoOrigem = request.TipoOrigem.ToString(),
            nomeArquivo = request.NomeArquivo,
            mimeType = request.MimeType,
            textoExtraido = normalizedText
        };

        IReadOnlyCollection<ImportSuggestionItem> items =
        [
            new ImportSuggestionItem(
                tipoSugestao,
                JsonSerializer.Serialize(payload, JsonOptions))
        ];

        return Task.FromResult(items);
    }

    private static decimal? ExtrairValor(string text)
    {
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

    [GeneratedRegex(@"\b\d{1,3}(?:\.\d{3})*,\d{2}\b|\b\d+\.\d{2}\b")]
    private static partial Regex ValorRegex();

    [GeneratedRegex(@"\b\d{4}-\d{2}-\d{2}\b")]
    private static partial Regex DataRegex();
}
