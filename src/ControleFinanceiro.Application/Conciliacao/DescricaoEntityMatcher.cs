using System.Text.RegularExpressions;

namespace ControleFinanceiro.Application.Conciliacao;

public static class DescricaoEntityMatcher
{
    private static readonly Regex GatewayPrefix = new(
        @"^(?:PAG|PAGTO|PAGSEGURO|MERCPAGO|MERCADOPAGO|MP|COMPRA|DEB|PGTO)\s*\*\s*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static (Guid? Id, double Score) EncontrarMelhorMatch(
        string descricao,
        IReadOnlyList<(Guid Id, string Nome)> entidades)
    {
        if (string.IsNullOrWhiteSpace(descricao) || entidades.Count == 0)
            return (null, 0);

        var descNorm = EstabelecimentoKey.Normalizar(descricao);
        var descLimpa = EstabelecimentoKey.Normalizar(GatewayPrefix.Replace(descricao, ""));

        if (descNorm.Length == 0 && descLimpa.Length == 0) return (null, 0);

        Guid? melhorId = null;
        double melhorScore = 0;

        foreach (var (id, nome) in entidades)
        {
            var nomeNorm = EstabelecimentoKey.Normalizar(nome);
            if (nomeNorm.Length < 2) continue;

            var score = CalcularSimilaridade(descNorm, descLimpa, nomeNorm);
            if (score > melhorScore)
            {
                melhorScore = score;
                melhorId = id;
            }
        }

        return (melhorId, melhorScore);
    }

    internal static double CalcularSimilaridade(string descNorm, string descLimpa, string nomeNorm)
    {
        if (descNorm == nomeNorm || descLimpa == nomeNorm)
            return 1.0;

        if (nomeNorm.Length >= 4)
        {
            if (descNorm.Contains(nomeNorm) || descLimpa.Contains(nomeNorm))
                return 0.95;
            if (descNorm.Length >= 4 && nomeNorm.Contains(descNorm))
                return 0.9;
            if (descLimpa.Length >= 4 && nomeNorm.Contains(descLimpa))
                return 0.9;
        }

        var desc = descLimpa.Length > 0 ? descLimpa : descNorm;
        var descTokens = Tokenizar(desc);
        var nomeTokens = Tokenizar(nomeNorm);

        if (descTokens.Count == 0 || nomeTokens.Count == 0) return 0;

        var nameTokensInDesc = nomeTokens.Count(t => descTokens.Contains(t));
        if (nameTokensInDesc == nomeTokens.Count && nomeTokens.Count >= 2)
            return 0.9;

        var intersection = descTokens.Intersect(nomeTokens).Count();
        var union = descTokens.Union(nomeTokens).Count();
        var jaccard = (double)intersection / union;

        return jaccard >= 0.5 ? Math.Min(jaccard * 0.85, 0.85) : 0;
    }

    private static HashSet<string> Tokenizar(string normalized)
    {
        return normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 2)
            .ToHashSet();
    }
}
