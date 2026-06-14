using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ControleFinanceiro.Application.Financeiro.Importacao;

public sealed record CsvFaturaItem(DateOnly DataTransacao, string Descricao, decimal Valor);

public static class CsvFaturaParser
{
    public sealed record ParseResult(
        IReadOnlyList<CsvFaturaItem> Itens,
        string? AvisoFormato);

    private static readonly string[] DateFormats =
        ["yyyy-MM-dd", "dd/MM/yyyy", "dd/MM/yy", "MM/dd/yyyy", "d/M/yyyy", "d/M/yy"];

    public static ParseResult Parse(string conteudo)
    {
        var linhas = conteudo
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();

        if (linhas.Count < 2)
            return new ParseResult([], "Arquivo CSV vazio ou sem dados.");

        var sep = DetectarSeparador(linhas[0]);
        var colunas = SplitLinha(linhas[0], sep);

        // Detecta qual coluna é data, descrição e valor pelo cabeçalho
        var idxData = EncontrarColuna(colunas, ["data", "date", "data da compra", "data transacao"]);
        var idxDescricao = EncontrarColuna(colunas, ["titulo", "título", "descricao", "descrição", "historico", "histórico", "estabelecimento", "description", "memo", "transaction"]);
        var idxValor = EncontrarColuna(colunas, ["valor", "value", "amount", "vlr", "valor (em r$)", "debit"]);

        string? aviso = null;

        // Fallback: tenta posição convencional se não encontrou pelo nome
        if (idxData < 0 || idxDescricao < 0 || idxValor < 0)
        {
            if (colunas.Length >= 3)
            {
                idxData = idxData < 0 ? 0 : idxData;
                idxDescricao = idxDescricao < 0 ? colunas.Length - 2 : idxDescricao;
                idxValor = idxValor < 0 ? colunas.Length - 1 : idxValor;
                aviso = "Formato não reconhecido. Colunas detectadas por posição (data=1ª, descrição=penúltima, valor=última).";
            }
            else
            {
                return new ParseResult([], "Não foi possível identificar as colunas de data, descrição e valor no CSV.");
            }
        }

        // Nubank tem coluna "Categoria" entre Data e Título — detecta e ignora
        var itens = new List<CsvFaturaItem>();

        foreach (var linha in linhas.Skip(1))
        {
            var campos = SplitLinha(linha, sep);
            if (campos.Length <= Math.Max(idxData, Math.Max(idxDescricao, idxValor)))
                continue;

            var dataStr = campos[idxData].Trim().Trim('"');
            var descStr = campos[idxDescricao].Trim().Trim('"');
            var valorStr = campos[idxValor].Trim().Trim('"');

            if (!TryParseData(dataStr, out var data)) continue;
            if (!TryParseValor(valorStr, out var valor)) continue;
            if (string.IsNullOrWhiteSpace(descStr)) continue;
            if (valor <= 0) continue; // ignora créditos/estornos

            itens.Add(new CsvFaturaItem(data, NormalizarDescricao(descStr), valor));
        }

        return new ParseResult(itens, aviso);
    }

    public static string GerarChave(CsvFaturaItem item) =>
        Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    $"{item.DataTransacao:yyyy-MM-dd}|{item.Descricao.ToUpperInvariant()}|{item.Valor:F2}")))
        [..16];

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static char DetectarSeparador(string linha)
    {
        var pontoVirgulas = linha.Count(c => c == ';');
        var virgulas = linha.Count(c => c == ',');
        var tabs = linha.Count(c => c == '\t');

        if (tabs > 0 && tabs >= pontoVirgulas && tabs >= virgulas) return '\t';
        if (pontoVirgulas > virgulas) return ';';
        return ',';
    }

    private static string[] SplitLinha(string linha, char sep)
    {
        // Respeita aspas duplas (campos com vírgula dentro)
        var resultado = new List<string>();
        var dentroAspas = false;
        var campo = new StringBuilder();

        foreach (var c in linha)
        {
            if (c == '"') { dentroAspas = !dentroAspas; continue; }
            if (c == sep && !dentroAspas) { resultado.Add(campo.ToString()); campo.Clear(); continue; }
            campo.Append(c);
        }
        resultado.Add(campo.ToString());
        return resultado.ToArray();
    }

    private static int EncontrarColuna(string[] colunas, string[] candidatos)
    {
        for (var i = 0; i < colunas.Length; i++)
        {
            var nome = colunas[i].Trim().Trim('"').ToLowerInvariant();
            if (candidatos.Any(c => nome.Contains(c)))
                return i;
        }
        return -1;
    }

    private static bool TryParseData(string s, out DateOnly result)
    {
        result = default;
        s = s.Trim();
        foreach (var fmt in DateFormats)
        {
            if (DateOnly.TryParseExact(s, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
                return true;
        }
        return false;
    }

    private static bool TryParseValor(string s, out decimal result)
    {
        result = 0;
        s = s.Trim().Replace("R$", "").Replace(" ", "");

        // Tenta formato BR (1.234,56) e US (1,234.56)
        if (decimal.TryParse(s, NumberStyles.Any, new CultureInfo("pt-BR"), out result))
            return result != 0;
        if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out result))
            return result != 0;
        return false;
    }

    private static string NormalizarDescricao(string s)
    {
        // Remove parcelamento "01/03" do final da descrição
        var normalizado = s.Trim();
        return normalizado.Length > 100 ? normalizado[..100] : normalizado;
    }
}
