using System.IO.Compression;
using System.Text;
using ControleFinanceiro.Application.ImportacoesWhatsapp;
using ControleFinanceiro.Infrastructure.ImportacoesWhatsapp;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace ControleFinanceiro.Infrastructure.Tests.ImportacoesWhatsapp;

public sealed class PdfDocumentExtractorTests
{
    [Fact]
    public async Task ExtractAsync_WhenPdfContainsBradescoInvoiceLayout_ShouldNormalizeCardInvoice()
    {
        var pdfBytes = CreatePlainTextPseudoPdf(
        [
            "XXXX.XXXX.XXXX.1111",
            "Aplicativo Bradesco Cartoes",
            "Data: 03/04/2026 - 07:54",
            "Vencimento",
            "13/04/2026",
            "Situacao do Extrato: FECHADO",
            "CLIENTE EXEMPLO - VISA INFINITE",
            "Data",
            "Historico",
            "Moeda",
            "de",
            "origem",
            "US$",
            "Cotacao",
            "US$",
            "R$",
            "31/03",
            "SUPERMERCADO MODELO",
            "258,55",
            "29/03",
            "AMAZON BR 1/2",
            "223,40",
            "23/03",
            "PAGTO. POR DEB EM C/C",
            "-481,95",
            "15/03",
            "SALDO ANTERIOR",
            "481,95",
            "07/03",
            "OPENAI *CHATGPT SUBSCR",
            "USD",
            "20,00",
            "20,00",
            "R$ 5,57",
            "111,40"
        ]);

        var pdfPath = CreateTempFile("fatura-bradesco.pdf", pdfBytes);
        var extractor = new DefaultDocumentExtractor(new FakeWebHostEnvironment());

        try
        {
            var result = await extractor.ExtractAsync(
                new DocumentExtractionRequest(null, "fatura-bradesco.pdf", "application/pdf", pdfPath),
                CancellationToken.None);

            result.Success.Should().BeTrue();
            result.Confianca.Should().BeGreaterThan(0.8m);
            result.TextoExtraido.Should().Contain("DOCUMENTO|FATURA_CARTAO");
            result.TextoExtraido.Should().Contain("EMISSOR|BRADESCO");
            result.TextoExtraido.Should().Contain("TITULAR|CLIENTE EXEMPLO");
            result.TextoExtraido.Should().Contain("VENCIMENTO|2026-04-13");
            result.TextoExtraido.Should().Contain("ITEM|2026-03-31|SUPERMERCADO MODELO|258,55");
            result.TextoExtraido.Should().Contain("ITEM|2026-03-29|AMAZON BR 1/2|223,40|PORTADOR=CLIENTE EXEMPLO|CARTAO_FINAL=1111|PARCELA=1/2");
            result.TextoExtraido.Should().Contain("ITEM|2026-03-07|OPENAI *CHATGPT SUBSCR|111,40|PORTADOR=CLIENTE EXEMPLO|CARTAO_FINAL=1111|MOEDA_ORIGEM=USD|VALOR_MOEDA_ORIGEM=20,00|COTACAO=5,57");
            result.TextoExtraido.Should().NotContain("PAGTO. POR DEB EM C/C");
            result.TextoExtraido.Should().NotContain("SALDO ANTERIOR");
        }
        finally
        {
            DeleteTempFile(pdfPath);
        }
    }

    [Fact]
    public async Task ExtractAsync_WhenBradescoInstallmentsComeInSeparateToken_ShouldKeepCorrectAmountAndInstallment()
    {
        var pdfBytes = CreatePlainTextPseudoPdf(
        [
            "XXXX.XXXX.XXXX.2892",
            "Aplicativo Bradesco Cartoes",
            "Data: 03/04/2026 - 07:54",
            "Situacao do Extrato: FECHADO",
            "CLIENTE EXEMPLO - VISA INFINITE",
            "27/02",
            "AMAZONMKTPLC*ENGAGEELE",
            "2/12",
            "212,59",
            "11/11",
            "MERCADOLIVRE*INFOARCOMERC",
            "5/7",
            "329,53",
            "27/06",
            "MERCADOPAGO*ECIDCURSOSONL",
            "10/12",
            "125,00",
            "12/12",
            "JIM.COM 30748782 MATHEUS 4/4",
            "1.073,53"
        ]);

        var pdfPath = CreateTempFile("fatura-bradesco-parcelada.pdf", pdfBytes);
        var extractor = new DefaultDocumentExtractor(new FakeWebHostEnvironment());

        try
        {
            var result = await extractor.ExtractAsync(
                new DocumentExtractionRequest(null, "fatura-bradesco-parcelada.pdf", "application/pdf", pdfPath),
                CancellationToken.None);

            result.Success.Should().BeTrue();
            result.TextoExtraido.Should().Contain("ITEM|2026-02-27|AMAZONMKTPLC*ENGAGEELE 2/12|212,59|PORTADOR=CLIENTE EXEMPLO|CARTAO_FINAL=2892|PARCELA=2/12");
            result.TextoExtraido.Should().Contain("ITEM|2025-11-11|MERCADOLIVRE*INFOARCOMERC 5/7|329,53|PORTADOR=CLIENTE EXEMPLO|CARTAO_FINAL=2892|PARCELA=5/7");
            result.TextoExtraido.Should().Contain("ITEM|2025-06-27|MERCADOPAGO*ECIDCURSOSONL 10/12|125,00|PORTADOR=CLIENTE EXEMPLO|CARTAO_FINAL=2892|PARCELA=10/12");
            result.TextoExtraido.Should().Contain("ITEM|2025-12-12|JIM.COM 30748782 MATHEUS 4/4|1073,53|PORTADOR=CLIENTE EXEMPLO|CARTAO_FINAL=2892|PARCELA=4/4");
        }
        finally
        {
            DeleteTempFile(pdfPath);
        }
    }

    [Fact]
    public async Task ExtractAsync_WhenPdfContainsNubankInvoiceLayout_ShouldNormalizeCardInvoice()
    {
        var pdfBytes = CreatePlainTextPseudoPdf(
        [
            "Ola, Cliente.",
            "Esta e a sua fatura de",
            "abril, no valor de",
            "R$ 2.260,73",
            "Data de vencimento:",
            "13 ABR 2026",
            "Periodo vigente:",
            "06 MAR a 06 ABR",
            "CLIENTE EXEMPLO",
            "FATURA",
            "13 ABR 2026",
            "EMISSAO E ENVIO",
            "06 ABR 2026",
            "RESUMO DA FATURA ATUAL",
            "Total a pagar",
            "R$ 2.260,73",
            "TRANSACOES",
            "DE 06 MAR A 06 ABR",
            "Cliente Exemplo",
            "R$ 1.045,75",
            "07 MAR",
            "**** 4835",
            "Skalla",
            "R$ 100,00",
            "14 MAR",
            "Uber - NuPay",
            "R$ 12,96",
            "14 MAR",
            "Estorno de Uber - NuPay",
            "-R$ 12,96",
            "Compras de Outro Responsavel",
            "R$ 82,84",
            "28 MAR",
            "**** 7950",
            "Petsupermark Order - Parcela 1/2",
            "R$ 82,84",
            "Pagamentos e Financiamentos",
            "-R$ 1.451,97"
        ]);

        var pdfPath = CreateTempFile("fatura-nubank.pdf", pdfBytes);
        var extractor = new DefaultDocumentExtractor(new FakeWebHostEnvironment());

        try
        {
            var result = await extractor.ExtractAsync(
                new DocumentExtractionRequest(null, "fatura-nubank.pdf", "application/pdf", pdfPath),
                CancellationToken.None);

            result.Success.Should().BeTrue();
            result.TextoExtraido.Should().Contain("EMISSOR|NUBANK");
            result.TextoExtraido.Should().Contain("VENCIMENTO|2026-04-13");
            result.TextoExtraido.Should().Contain("PERIODO_INICIO|2026-03-06");
            result.TextoExtraido.Should().Contain("PERIODO_FIM|2026-04-06");
            result.TextoExtraido.Should().Contain("TOTAL|2260,73");
            result.TextoExtraido.Should().Contain("ITEM|2026-03-07|Skalla|100,00|PORTADOR=Cliente Exemplo|CARTAO_FINAL=4835");
            result.TextoExtraido.Should().Contain("ITEM|2026-03-14|Estorno de Uber - NuPay|-12,96|PORTADOR=Cliente Exemplo|ESTORNO=true");
            result.TextoExtraido.Should().Contain("ITEM|2026-03-28|Petsupermark Order - Parcela 1/2|82,84|PORTADOR=Outro Responsavel|CARTAO_FINAL=7950|PARCELA=1/2");
            result.TextoExtraido.Should().NotContain("Pagamentos e Financiamentos");
        }
        finally
        {
            DeleteTempFile(pdfPath);
        }
    }

    [Fact]
    public async Task ExtractAsync_WhenNubankHeaderAppearsAfterTransactions_ShouldIgnoreHeaderAsTransaction()
    {
        var pdfBytes = CreatePlainTextPseudoPdf(
        [
            "Ola, Cliente.",
            "Esta e a sua fatura de",
            "abril, no valor de",
            "R$ 2.260,73",
            "Data de vencimento:",
            "13 ABR 2026",
            "Periodo vigente:",
            "06 MAR a 06 ABR",
            "MICHELLE RIBEIRO MACEDO",
            "FATURA",
            "13 ABR 2026",
            "RESUMO DA FATURA ATUAL",
            "TRANSACOES",
            "Michelle R Macedo",
            "R$ 100,00",
            "07 MAR",
            "**** 4835",
            "Skalla",
            "R$ 100,00",
            "Matheus Ferreira",
            "R$ 44,00",
            "05 ABR",
            "Coms e Bebs",
            "R$ 44,00",
            "13 ABR 2026",
            "EMISSAO E ENVIO",
            "06 ABR 2026",
            "Pagamentos e Financiamentos",
            "-R$ 1.451,97"
        ]);

        var pdfPath = CreateTempFile("fatura-nubank-header-fora-de-ordem.pdf", pdfBytes);
        var extractor = new DefaultDocumentExtractor(new FakeWebHostEnvironment());

        try
        {
            var result = await extractor.ExtractAsync(
                new DocumentExtractionRequest(null, "fatura-nubank-header-fora-de-ordem.pdf", "application/pdf", pdfPath),
                CancellationToken.None);

            result.Success.Should().BeTrue();
            result.TextoExtraido.Should().Contain("ITEM|2026-03-07|Skalla|100,00|PORTADOR=Michelle R Macedo|CARTAO_FINAL=4835");
            result.TextoExtraido.Should().Contain("ITEM|2026-04-05|Coms e Bebs|44,00|PORTADOR=Matheus Ferreira");
            result.TextoExtraido.Should().NotContain("EMISSAO E ENVIO");
        }
        finally
        {
            DeleteTempFile(pdfPath);
        }
    }

    [Fact]
    public async Task ExtractAsync_WhenNubankTransactionHasDecorativeTokenBetweenDateAndDescription_ShouldKeepInstallmentItem()
    {
        var pdfBytes = CreatePlainTextPseudoPdf(
        [
            "Ola, Cliente.",
            "Data de vencimento:",
            "13 ABR 2026",
            "Periodo vigente:",
            "06 MAR a 06 ABR",
            "MICHELLE RIBEIRO MACEDO",
            "FATURA",
            "13 ABR 2026",
            "RESUMO DA FATURA ATUAL",
            "Total a pagar",
            "R$ 2.260,73",
            "TRANSACOES",
            "Michelle Ribeiro Macedo",
            "R$ 4.060,61",
            "07 MAR",
            "Skalla",
            "R$ 100,00",
            "Pagamentos e Financiamentos",
            "13 MAR",
            "Pagamento em 13 MAR",
            "-R$ 1.959,55",
            "06 MAR",
            "↳",
            "ESTADO DE MINAS GERAIS - Parcela 6/8",
            "Total a pagar",
            ": R$ 4.060,61 (valor da transacao de R$ 3.509,63 + R$ 54,91 de",
            "IOF + R$ 496,08 de juros) divididos em 8 parcelas de R$ 507,58.",
            "Total a pagar: R$ 4.060,61 (valor da transacao de R$ 3.509,63 + R$ 54,91 de IOF + R$ 496,08 de juros) divididos em 8 parcelas de R$ 507,58.",
            "R$ 507,58",
            "Proximas Faturas",
            "Fechamento da proxima fatura",
            "06 MAI 2026",
            "-R$ 1.451,97"
        ]);

        var pdfPath = CreateTempFile("fatura-nubank-parcela-com-icone.pdf", pdfBytes);
        var extractor = new DefaultDocumentExtractor(new FakeWebHostEnvironment());

        try
        {
            var result = await extractor.ExtractAsync(
                new DocumentExtractionRequest(null, "fatura-nubank-parcela-com-icone.pdf", "application/pdf", pdfPath),
                CancellationToken.None);

            result.Success.Should().BeTrue();
            result.TextoExtraido.Should().Contain("ITEM|2026-03-07|Skalla|100,00|PORTADOR=Michelle Ribeiro Macedo");
            result.TextoExtraido.Should().Contain("ITEM|2026-03-06|ESTADO DE MINAS GERAIS - Parcela 6/8|507,58|PORTADOR=Michelle Ribeiro Macedo|PARCELA=6/8");
            result.TextoExtraido.Should().NotContain("↳");
            result.TextoExtraido.Should().NotContain("Pagamento em 13 MAR");
            result.TextoExtraido.Should().NotContain("Fechamento da proxima fatura");
        }
        finally
        {
            DeleteTempFile(pdfPath);
        }
    }

    [Fact]
    public async Task ExtractAsync_WhenPdfUsesToUnicodeMap_ShouldDecodeEmbeddedText()
    {
        var pdfBytes = CreateToUnicodePseudoPdf("OLA PDF");
        var pdfPath = CreateTempFile("fatura-tounicode.pdf", pdfBytes);
        var extractor = new DefaultDocumentExtractor(new FakeWebHostEnvironment());

        try
        {
            var result = await extractor.ExtractAsync(
                new DocumentExtractionRequest(null, "fatura-tounicode.pdf", "application/pdf", pdfPath),
                CancellationToken.None);

            result.Success.Should().BeTrue();
            result.TextoExtraido.Should().Contain("OLA PDF");
        }
        finally
        {
            DeleteTempFile(pdfPath);
        }
    }

    private static byte[] CreatePlainTextPseudoPdf(IReadOnlyCollection<string> lines)
    {
        var content = new StringBuilder();
        content.AppendLine("BT");
        content.AppendLine("/F1 12 Tf");
        content.AppendLine("1 0 0 1 0 0 Tm");

        foreach (var line in lines)
        {
            content.Append('(')
                .Append(EscapePdfLiteral(line))
                .AppendLine(") Tj");
        }

        content.AppendLine("ET");

        return Encoding.Latin1.GetBytes(
            """
            %PDF-1.4
            1 0 obj
            << /Type /Catalog >>
            endobj
            2 0 obj
            << /Length 0 >>
            stream
            """ + content + """
            endstream
            endobj
            """);
    }

    private static byte[] CreateToUnicodePseudoPdf(string text)
    {
        var uniqueChars = text
            .Distinct()
            .OrderBy(character => character)
            .ToArray();

        var cmap = new StringBuilder()
            .AppendLine("/CIDInit /ProcSet findresource begin")
            .AppendLine("12 dict begin")
            .AppendLine("begincmap")
            .AppendLine("/CIDSystemInfo << /Registry (TTX+0) /Ordering (T42UV) /Supplement 0 >> def")
            .AppendLine("/CMapName /TTX+0 def")
            .AppendLine("/CMapType 2 def")
            .AppendLine("1 begincodespacerange")
            .AppendLine("<0000><FFFF>")
            .AppendLine("endcodespacerange")
            .AppendLine($"{uniqueChars.Length} beginbfchar");

        var reverseMap = new Dictionary<char, ushort>();
        ushort code = 1;
        foreach (var character in uniqueChars)
        {
            reverseMap[character] = code;
            cmap.AppendLine($"<{code:X4}><{(ushort)character:X4}>");
            code++;
        }

        cmap.AppendLine("endbfchar")
            .AppendLine("endcmap")
            .AppendLine("CMapName currentdict /CMap defineresource pop")
            .AppendLine("end end");

        var encodedTextBytes = new List<byte>();
        foreach (var character in text)
        {
            var mappedCode = reverseMap[character];
            encodedTextBytes.Add((byte)(mappedCode >> 8));
            encodedTextBytes.Add((byte)(mappedCode & 0xFF));
        }

        var contentBytes = new List<byte>();
        contentBytes.AddRange(Encoding.ASCII.GetBytes("BT\n/F2 12 Tf\n("));
        contentBytes.AddRange(encodedTextBytes);
        contentBytes.AddRange(Encoding.ASCII.GetBytes(") Tj\nET\n"));

        var compressedCmap = Compress(Encoding.ASCII.GetBytes(cmap.ToString()));
        var compressedContent = Compress(contentBytes.ToArray());

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
        {
            writer.Write(Encoding.ASCII.GetBytes("%PDF-1.5\n"));
            writer.Write(Encoding.ASCII.GetBytes("1 0 obj\n<< /ToUnicode 2 0 R >>\nendobj\n"));
            writer.Write(Encoding.ASCII.GetBytes($"2 0 obj\n<< /Length {compressedCmap.Length} /Filter /FlateDecode >>\nstream\n"));
            writer.Write(compressedCmap);
            writer.Write(Encoding.ASCII.GetBytes("\nendstream\nendobj\n"));
            writer.Write(Encoding.ASCII.GetBytes($"3 0 obj\n<< /Length {compressedContent.Length} /Filter /FlateDecode >>\nstream\n"));
            writer.Write(compressedContent);
            writer.Write(Encoding.ASCII.GetBytes("\nendstream\nendobj\n"));
        }

        return stream.ToArray();
    }

    private static byte[] Compress(byte[] bytes)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(bytes, 0, bytes.Length);
        }

        return output.ToArray();
    }

    private static string EscapePdfLiteral(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
    }

    private static string CreateTempFile(string fileName, byte[] bytes)
    {
        var directory = Path.Combine(Path.GetTempPath(), "controle-financeiro-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, fileName);
        File.WriteAllBytes(filePath, bytes);
        return filePath;
    }

    private static void DeleteTempFile(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (directory is not null && Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }

    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "ControleFinanceiro.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
