using System.Globalization;
using System.Text;

namespace ControleFinanceiro.Application.ImportacoesWhatsapp;

public interface IHeuristicMatchingService
{
    HistoricalPredictionData? TentarPredicaoHeuristicaCompraCartao(
        ImportacaoWhatsappSuggestionPayload payload,
        IReadOnlyCollection<ContaGerencialHeuristicaData> contas);
}

public sealed class HeuristicMatchingService : IHeuristicMatchingService
{
    public HistoricalPredictionData? TentarPredicaoHeuristicaCompraCartao(
        ImportacaoWhatsappSuggestionPayload payload,
        IReadOnlyCollection<ContaGerencialHeuristicaData> contas)
    {
        if (contas.Count == 0)
        {
            return null;
        }

        var textoNormalizado = NormalizarTextoHeuristico(payload.Descricao);
        if (string.IsNullOrWhiteSpace(textoNormalizado))
        {
            return null;
        }

        var conta = EncontrarContaHeuristica(contas, textoNormalizado);
        if (conta is null)
        {
            return null;
        }

        return new HistoricalPredictionData(
            conta.Id,
            conta.ResponsavelPadraoId,
            null,
            false,
            false,
            0,
            0.67m);
    }

    private static ContaGerencialHeuristicaData? EncontrarContaHeuristica(
        IReadOnlyCollection<ContaGerencialHeuristicaData> contas,
        string textoNormalizado)
    {
        return EncontrarContaPorCategoria(contas, textoNormalizado, ["SUPERMERCADO"], ["SUPERMERC", "SUPERKIT", "SUPERMERCADOS", "SACOLAO"])
            ?? EncontrarContaPorCategoria(contas, textoNormalizado, ["FARMACIA", "DROGARIA"], ["FARMAC", "DROGAR", "ARAUJO", "PACHECO", "ULTRAFARMA", "PAGUE MENOS", "DROGA RAIA"])
            ?? EncontrarContaPorCategoria(contas, textoNormalizado, ["COMBUST", "POSTO"], ["POSTO", "COMBUST", "IPIRANGA", "SHELL", "ALE", "PETROBRAS"])
            ?? EncontrarContaPorCategoria(contas, textoNormalizado, ["APP TAXI", "TAXI", "ONIBUS", "TRANSPORTE"], ["UBER", "99", "CABIFY", "TAXI"])
            ?? EncontrarContaPorCategoria(contas, textoNormalizado, ["LANCHES", "DELIVERY"], ["IFOOD", "RAPPI", "DELIVERY", "PIZZA", "BURGER", "BURGUER", "LANCH"])
            ?? EncontrarContaPorCategoria(contas, textoNormalizado, ["RESTAUR"], ["RESTAUR", "SKALLA", "CHURRASC"]);
    }

    private static ContaGerencialHeuristicaData? EncontrarContaPorCategoria(
        IReadOnlyCollection<ContaGerencialHeuristicaData> contas,
        string textoNormalizado,
        IReadOnlyCollection<string> marcadoresConta,
        IReadOnlyCollection<string> marcadoresLancamento)
    {
        if (!marcadoresLancamento.Any(marcador => textoNormalizado.Contains(marcador, StringComparison.Ordinal)))
        {
            return null;
        }

        return contas.FirstOrDefault(conta =>
        {
            var descricaoNormalizada = NormalizarTextoHeuristico(conta.Descricao);
            return marcadoresConta.Any(marcador => descricaoNormalizada.Contains(marcador, StringComparison.Ordinal));
        });
    }

    private static string NormalizarTextoHeuristico(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(valor.Length);
        var normalized = valor.Normalize(NormalizationForm.FormD);

        foreach (var caractere in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caractere) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(caractere) ? char.ToUpperInvariant(caractere) : ' ');
        }

        return string.Join(
            ' ',
            builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}