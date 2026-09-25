using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UglyToad.PdfPig;
using ControleFinanceiro.Application.Financeiro.Importacao;

namespace ControleFinanceiro.Infrastructure.ImportacoesWhatsapp;

public sealed class BradescoPdfFaturaReader(IInvoiceImageOcr? ocr = null) : IPdfFaturaReader
{
    private static readonly SemaphoreSlim ImageReadLock = new(1, 1);

    public async Task<CsvFaturaParser.ParseResult> ParseAsync(Stream stream, CancellationToken cancellationToken, DateOnly? vencimentoSelecionado = null)
    {
        try
        {
            var imageResult = await ReadImagesAsync(stream, vencimentoSelecionado, cancellationToken);
            if (imageResult is not null) return imageResult;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new([], "Não foi possível ler as imagens do PDF. Verifique se o arquivo está legível e sem senha. Nenhum lançamento foi criado.");
        }
        stream.Position = 0;
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var bytes = buffer.ToArray();
        try
        {
            var mensal = BradescoMensalPdfParser.Parse(bytes);
            if (mensal is not null) return mensal;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Failure();
        }
        buffer.Position = 0;
        var text = PdfFaturaParser.ExtrairTexto(buffer);
        var rows = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var tokens = PdfTextTokenizer.ExtractTokens(bytes);
        if (!tokens.Concat(rows).Any(t => t.Contains("Bradesco", StringComparison.OrdinalIgnoreCase)))
        {
            buffer.Position = 0;
            return PdfFaturaParser.Parse(buffer);
        }
        try
        {
            foreach (var candidate in new IReadOnlyCollection<string>[] { rows, tokens })
            {
                if (!CardInvoiceTextNormalizer.TryNormalize(candidate, out var normalized)) continue;
                var parsed = ParseNormalized(normalized);
                if (parsed.Itens.Count > 0) return parsed;
            }
            return Failure();
        }
        catch (ArgumentException) { return Failure(); }
    }

    private async Task<CsvFaturaParser.ParseResult?> ReadImagesAsync(Stream stream, DateOnly? due, CancellationToken ct)
    {
        await ImageReadLock.WaitAsync(ct);
        try
        {
            stream.Position = 0;
            using var document = PdfDocument.Open(stream);
            if (document.NumberOfPages > 20) return new([], "O PDF deve ter no máximo 20 páginas.");
            if (document.GetPages().Any(p => p.Text.Any(char.IsLetterOrDigit))) return null;
            if (ocr is null) return new([], "Este PDF contém imagens. O serviço de OCR não está disponível.");
            var text = new StringBuilder();
            var tallImages = new HashSet<string>();
            foreach (var page in document.GetPages())
            {
                ct.ThrowIfCancellationRequested();
                var images = page.GetImages().Where(x => !x.IsImageMask).ToArray();
                if (images.Length != 1) return new([], "Não foi possível reconhecer o layout de imagens deste PDF.");
                var image = images[0];
                if ((long)image.WidthInSamples * image.HeightInSamples > 40_000_000 || image.WidthInSamples > 5000)
                    return new([], "A resolução da imagem excede o limite de leitura. Exporte o PDF em resolução menor.");
                // Imagem longa repetida e recortada nas páginas pelo aplicativo do banco.
                // Não deduplicar páginas/compras iguais quando forem imagens independentes.
                if (image.Bounds.Height > page.Height * 1.5)
                {
                    var hash = Convert.ToHexString(SHA256.HashData(image.RawBytes.ToArray()));
                    if (!tallImages.Add(hash)) continue;
                }
                if (!image.TryGetPng(out var png)) return new([], "Não foi possível extrair a imagem da fatura.");
                text.AppendLine(await ocr.ReadAsync(png, ct));
            }
            return BradescoAbertoOcrParser.Parse(text.ToString(), due);
        }
        finally { ImageReadLock.Release(); }
    }

    internal static CsvFaturaParser.ParseResult ParseNormalized(string normalized)
    {
        var lines = normalized.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var due = lines.FirstOrDefault(l => l.StartsWith("VENCIMENTO|", StringComparison.Ordinal))?.Split('|')[1];
        if (!DateOnly.TryParseExact(due, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var vencimento))
            return Failure();
        var items = new List<CsvFaturaItem>();
        foreach (var line in lines.Where(l => l.StartsWith("ITEM|", StringComparison.Ordinal)))
        {
            var fields = line.Split('|');
            if (fields.Length < 4 || !DateOnly.TryParseExact(fields[1], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data)
                || !decimal.TryParse(fields[3], NumberStyles.Number, CultureInfo.GetCultureInfo("pt-BR"), out var valor)
                || valor == 0 || data > vencimento)
                return Failure();
            var current = 1;
            var total = 1;
            var installment = fields.Skip(4).FirstOrDefault(f => f.StartsWith("PARCELA=", StringComparison.Ordinal));
            if (installment is not null)
            {
                var parts = installment[8..].Split('/');
                if (parts.Length != 2 || !int.TryParse(parts[0], out current) || !int.TryParse(parts[1], out total)
                    || current < 1 || total < current || total > 120) return Failure();
            }
            // O valor é o da parcela presente na fatura, sem gerar outras parcelas por inferência.
            items.Add(new CsvFaturaItem(data, fields[2], valor, vencimento, current, total));
        }
        return items.Count == 0 ? Failure() : new CsvFaturaParser.ParseResult(items,
            "Fatura Bradesco lida sem IA. Confira os itens, estornos e o total com o PDF antes de confirmar. Pagamentos e saldo anterior não são novas compras.");
    }

    private static CsvFaturaParser.ParseResult Failure() => new([], 
        "Não foi possível interpretar com segurança a fatura Bradesco. Verifique se o PDF baixado contém texto e o vencimento completo. Nenhum lançamento foi criado.");
}
