using System.Text;
using ClosedXML.Excel;
using ControleFinanceiro.Application.FinanceAI;
using ControleFinanceiro.Infrastructure.ImportacoesWhatsapp;

namespace ControleFinanceiro.Infrastructure.FinanceAI;

public sealed class DocumentReaderService : IDocumentReaderService
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };

    public async Task<DocumentReadResult> LerAsync(string caminhoArquivo, string? nomeArquivo, CancellationToken cancellationToken)
    {
        if (!File.Exists(caminhoArquivo))
            return new DocumentReadResult(false, null, null, 0, "Arquivo não encontrado.");

        var ext = Path.GetExtension(nomeArquivo ?? caminhoArquivo).ToLowerInvariant();

        if (ImageExtensions.Contains(ext))
            return new DocumentReadResult(false, null, "imagem", 0, "Use vision para analisar imagens, não esta ferramenta.");

        return ext switch
        {
            ".pdf" => await LerPdfAsync(caminhoArquivo, cancellationToken),
            ".xlsx" or ".xls" => LerExcel(caminhoArquivo),
            ".csv" => await LerTextoAsync(caminhoArquivo, "csv", cancellationToken),
            ".ofx" => await LerTextoAsync(caminhoArquivo, "ofx", cancellationToken),
            ".txt" or ".text" or ".log" => await LerTextoAsync(caminhoArquivo, "texto", cancellationToken),
            ".json" => await LerTextoAsync(caminhoArquivo, "json", cancellationToken),
            ".xml" => await LerTextoAsync(caminhoArquivo, "xml", cancellationToken),
            _ => await TentarLerComoTextoAsync(caminhoArquivo, ext, cancellationToken)
        };
    }

    private static async Task<DocumentReadResult> LerPdfAsync(string path, CancellationToken ct)
    {
        var bytes = await File.ReadAllBytesAsync(path, ct);
        var tokens = PdfTextTokenizer.ExtractTokens(bytes);

        if (tokens.Count == 0)
            return new DocumentReadResult(false, null, "pdf", 0, "PDF não contém texto legível. Se for um PDF escaneado, envie como imagem.");

        var texto = string.Join(Environment.NewLine, tokens);
        var linhas = texto.Split('\n').Length;

        return new DocumentReadResult(true, texto, "pdf", linhas);
    }

    private static DocumentReadResult LerExcel(string path)
    {
        try
        {
            using var workbook = new XLWorkbook(path);
            var sb = new StringBuilder();
            var totalLinhas = 0;

            foreach (var ws in workbook.Worksheets)
            {
                sb.AppendLine($"=== Planilha: {ws.Name} ===");
                var usedRange = ws.RangeUsed();
                if (usedRange is null)
                {
                    sb.AppendLine("(vazia)");
                    continue;
                }

                var firstRow = usedRange.FirstRow().RowNumber();
                var lastRow = usedRange.LastRow().RowNumber();
                var firstCol = usedRange.FirstColumn().ColumnNumber();
                var lastCol = usedRange.LastColumn().ColumnNumber();

                for (var row = firstRow; row <= lastRow && row <= firstRow + 500; row++)
                {
                    var cells = new List<string>();
                    for (var col = firstCol; col <= lastCol; col++)
                    {
                        var cell = ws.Cell(row, col);
                        cells.Add(cell.GetFormattedString());
                    }
                    sb.AppendLine(string.Join(" | ", cells));
                    totalLinhas++;
                }

                if (lastRow - firstRow > 500)
                    sb.AppendLine($"[... mais {lastRow - firstRow - 500} linhas omitidas ...]");

                sb.AppendLine();
            }

            return new DocumentReadResult(true, sb.ToString().Trim(), "excel", totalLinhas);
        }
        catch (Exception ex)
        {
            return new DocumentReadResult(false, null, "excel", 0, $"Erro ao ler Excel: {ex.Message}");
        }
    }

    private static async Task<DocumentReadResult> LerTextoAsync(string path, string formato, CancellationToken ct)
    {
        var texto = await File.ReadAllTextAsync(path, Encoding.UTF8, ct);
        if (string.IsNullOrWhiteSpace(texto))
            return new DocumentReadResult(false, null, formato, 0, "Arquivo está vazio.");

        var linhas = texto.Split('\n').Length;
        return new DocumentReadResult(true, texto.Trim(), formato, linhas);
    }

    private static async Task<DocumentReadResult> TentarLerComoTextoAsync(string path, string ext, CancellationToken ct)
    {
        try
        {
            var bytes = await File.ReadAllBytesAsync(path, ct);
            if (bytes.Length > 0 && bytes.Take(512).Any(b => b == 0))
                return new DocumentReadResult(false, null, ext.TrimStart('.'), 0,
                    $"Formato '{ext}' não suportado para leitura direta. Formatos suportados: PDF, Excel (.xlsx), CSV, OFX, texto.");

            var texto = Encoding.UTF8.GetString(bytes).Trim();
            if (string.IsNullOrWhiteSpace(texto))
                return new DocumentReadResult(false, null, ext.TrimStart('.'), 0, "Arquivo está vazio.");

            var linhas = texto.Split('\n').Length;
            return new DocumentReadResult(true, texto, ext.TrimStart('.'), linhas);
        }
        catch
        {
            return new DocumentReadResult(false, null, ext.TrimStart('.'), 0,
                $"Não foi possível ler o arquivo com extensão '{ext}'.");
        }
    }
}
