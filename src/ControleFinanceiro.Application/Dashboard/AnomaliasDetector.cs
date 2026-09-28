using ControleFinanceiro.Contracts.Dashboard;

namespace ControleFinanceiro.Application.Dashboard;

public sealed record AnomaliaConta(Guid Id, DateOnly Data, string Descricao, Guid RecebedorId,
    Guid? ResponsavelId, Guid? CartaoId, Guid? ContaBancariaId, decimal Valor, Guid? RecorrenciaId);

public static class AnomaliasDetector
{
    public static IReadOnlyList<DashboardAnomaliaItem> Detectar(IReadOnlyList<AnomaliaConta> contas, DateOnly mes)
    {
        var inicio = new DateOnly(mes.Year, mes.Month, 1);
        var fim = inicio.AddMonths(1);
        var elegiveis = contas.Where(c => c.Valor > 0 && c.Data >= inicio.AddMonths(-3) && c.Data < fim
            && !string.IsNullOrWhiteSpace(c.Descricao)).ToArray();
        var alertas = new List<DashboardAnomaliaItem>();
        foreach (var grupo in elegiveis.GroupBy(c => new
                 {
                     Descricao = string.Join(" ", c.Descricao.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant(),
                     c.RecebedorId, c.ResponsavelId, c.CartaoId, c.ContaBancariaId
                 }))
        {
            var atuais = grupo.Where(c => c.Data >= inicio).OrderBy(c => c.Data).ThenBy(c => c.Id).ToArray();
            foreach (var duplicadas in atuais.GroupBy(c => new { c.Data, c.Valor }).Where(g => g.Count() > 1))
                Adicionar("DuplicidadeProvavel", "Mesma descrição, recebedor, responsável, conta/cartão, data e valor. Pode ser legítimo; confira os registros.",
                    duplicadas.First(), null, duplicadas);

            // Mais de uma cobrança no mesmo mês torna a comparação ambígua.
            if (atuais.Length != 1 || grupo.Select(c => c.RecorrenciaId).Distinct().Count() != 1) continue;
            var historico = grupo.Where(c => c.Data < inicio).OrderBy(c => c.Data).ToArray();
            if (historico.Length != 3 || historico.Select(c => new { c.Data.Year, c.Data.Month }).Distinct().Count() != 3)
                continue;
            var atual = atuais[0];
            var mediana = historico.Select(c => c.Valor).Order().ElementAt(1);
            var evidencias = historico.Append(atual).ToArray();
            if (atual.RecorrenciaId is not null)
            {
                if (atual.Valor >= mediana * 1.20m && atual.Valor - mediana >= 10m)
                    Adicionar("AumentoRecorrente", "Ao menos 20% e R$ 10 acima da mediana dos três meses anteriores, com uma cobrança por mês da mesma recorrência.", atual, mediana, evidencias);
                Adicionar("RevisarRecorrencia", "Recorrência com cobranças em quatro meses consecutivos. Confirme se ainda é necessária; não há informação de uso do serviço.", atual, mediana, evidencias);
            }
            else if (atual.Valor >= mediana * 1.50m && atual.Valor - mediana >= 50m)
                Adicionar("ValorIncomum", "Ao menos 50% e R$ 50 acima da mediana dos três meses anteriores, com uma cobrança por mês da mesma descrição e contexto.", atual, mediana, evidencias);
        }
        return alertas.OrderBy(a => a.Tipo, StringComparer.Ordinal).ThenBy(a => a.Id, StringComparer.Ordinal).ToArray();

        void Adicionar(string tipo, string regra, AnomaliaConta atual, decimal? valorBase, IEnumerable<AnomaliaConta> evidencias)
        {
            var ordenadas = evidencias.OrderBy(c => c.Data).ThenBy(c => c.Id).ToArray();
            alertas.Add(new($"{tipo}:{atual.Id}", tipo, atual.Descricao.Trim(), regra, atual.Valor, valorBase,
                ordenadas.Select(c => new DashboardAnomaliaEvidencia(c.Id, c.Data, c.Valor)).ToArray()));
        }
    }
}
