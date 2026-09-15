using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ControleFinanceiro.Application.Conciliacao;

public sealed record CompraComparavel(Guid Id, DateOnly Data, string Descricao, decimal Valor, int NumeroParcela, int QuantidadeParcelas);
public sealed record CorrespondenciaFatura(Guid ContaId, decimal Diferenca, int Pontos, string Motivo, bool CorrespondenciaClara);

public static class EstabelecimentoKey
{
    public static string Normalizar(string descricao)
    {
        var text = descricao.Normalize(NormalizationForm.FormD);
        text = new string(text.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()).ToUpperInvariant();
        text = Regex.Replace(text, @"(?:JAN|FEV|MAR|ABR|MAI|JUN|JUL|AGO|SET|OUT|NOV|DEZ)\s*\d{2,4}\s*$", "");
        text = Regex.Replace(text, @"\s*\d{1,2}/\d{1,3}\s*$", "");
        return Regex.Replace(text, @"[^A-Z0-9]+", " ").Trim();
    }
}

public static class FaturaMatching
{
    public static IReadOnlyList<CorrespondenciaFatura> Encontrar(CompraComparavel item, IEnumerable<CompraComparavel> contas,
        string? descricaoAprendida = null, decimal tolerancia = 0.05m)
    {
        if (tolerancia < 0 || tolerancia > 1m) throw new ArgumentOutOfRangeException(nameof(tolerancia));
        var original = EstabelecimentoKey.Normalizar(item.Descricao);
        var alias = string.IsNullOrWhiteSpace(descricaoAprendida) ? null : EstabelecimentoKey.Normalizar(descricaoAprendida);
        var candidates = new List<CorrespondenciaFatura>();
        foreach (var conta in contas)
        {
            var difference = item.Valor - conta.Valor;
            var days = Math.Abs(item.Data.DayNumber - conta.Data.DayNumber);
            if (Math.Sign(item.Valor) != Math.Sign(conta.Valor) || Math.Abs(difference) > tolerancia || days > 3 ||
                item.NumeroParcela != conta.NumeroParcela || item.QuantidadeParcelas != conta.QuantidadeParcelas) continue;
            var name = EstabelecimentoKey.Normalizar(conta.Descricao);
            var identified = original.Length > 0 && (name == original || name == alias);
            var points = (difference == 0 ? 40 : 35) + (days == 0 ? 30 : 20) + (identified ? 30 : 0);
            var reason = (difference == 0 ? "Mesmo valor" : "Diferença de centavos") +
                (days == 0 ? ", mesma data" : ", data próxima") + (identified ? ", estabelecimento reconhecido" : ", conferir estabelecimento");
            candidates.Add(new(conta.Id, difference, points, reason, identified));
        }
        // A correspondência é apenas uma sugestão. Empate ou candidatos concorrentes exigem escolha humana.
        var clearCount = candidates.Count(c => c.CorrespondenciaClara);
        return candidates.OrderByDescending(c => c.Pontos).ThenBy(c => c.ContaId)
            .Select(c => c with { CorrespondenciaClara = c.CorrespondenciaClara && clearCount == 1 }).ToArray();
    }
}
