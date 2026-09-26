using ControleFinanceiro.Infrastructure.ImportacoesWhatsapp;
using FluentAssertions;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;

namespace ControleFinanceiro.Infrastructure.Tests.ImportacoesWhatsapp;

public class BradescoImageReaderTests
{
    private const string Text = "Aplicativo Bradesco\nData: 25/09/2026\nSituação do Extrato: Em Aberto\n22/09 LOJA TESTE USD 0,00 R$ 0,00 R$ 100,00\n22/09 LOJA TESTE USD 0,00 R$ 0,00 R$ 100,00\n21/09 ESTORNO TESTE USD 0,00 R$ 0,00 R$ -10,00\nTotal para: PESSOA TESTE R$ 190,00";
    private static byte[] Image() => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "ImportacoesWhatsapp", "Fixtures", "bradesco-aberto-sintetico.png"));
    private static MemoryStream Pdf(int pages = 3)
    {
        var builder = new PdfDocumentBuilder();
        var png = Image();
        for (var i = 0; i < pages; i++)
            builder.AddPage(600, 800).AddPng(png, new PdfRectangle(0, -1600 + i * 800, 600, 800 + i * 800));
        return new MemoryStream(builder.Build());
    }

    [Fact]
    public async Task ImagemLongaRepetida_LeUmaVezSemPerderComprasIguais()
    {
        var engine = new FakeOcr();
        using var pdf = Pdf();
        var result = await new BradescoPdfFaturaReader(engine).ParseAsync(pdf, default, new DateOnly(2026,10,20));
        result.Itens.Select(x => x.Valor).Should().Equal(100,100,-10);
        engine.Calls.Should().Be(1);
    }

    [Fact]
    public async Task FalhaDoOcrNaoCriaLeituraParcialELiberaProximaLeitura()
    {
        var engine = new FakeOcr { Fail = true };
        using var pdf = Pdf();
        var reader = new BradescoPdfFaturaReader(engine);
        (await reader.ParseAsync(pdf, default, new DateOnly(2026,10,20))).Itens.Should().BeEmpty();
        engine.Fail = false;
        pdf.Position = 0;
        (await reader.ParseAsync(pdf, default, new DateOnly(2026,10,20))).Itens.Should().HaveCount(3);
    }

    [Fact]
    public async Task PdfAcimaDeVintePaginasNaoAcionaOcr()
    {
        using var pdf = Pdf(21);
        var engine = new FakeOcr();
        (await new BradescoPdfFaturaReader(engine).ParseAsync(pdf, default, new DateOnly(2026,10,20))).Itens.Should().BeEmpty();
        engine.Calls.Should().Be(0);
    }

    [NativeOcrFact]
    public async Task RuntimeNativoReconheceImagemSinteticaEConfereSubtotal()
    {
        var engine = new TesseractImageOcr(Options.Create(new InvoiceOcrOptions()));
        var text = await engine.ReadAsync(Image(), default);
        var result = BradescoAbertoOcrParser.Parse(text, new DateOnly(2026,10,20));
        result.Itens.Select(x => x.Valor).Should().Equal(100,100,-10);
    }

    private class FakeOcr : IInvoiceImageOcr
    {
        public int Calls { get; private set; }
        public bool Fail { get; set; }
        public Task<string> ReadAsync(byte[] png, CancellationToken cancellationToken)
        {
            Calls++;
            if (Fail) throw new InvalidOperationException();
            return Task.FromResult(Text);
        }
    }
}

public sealed class NativeOcrFactAttribute : FactAttribute
{
    public NativeOcrFactAttribute()
    {
        if (!OperatingSystem.IsLinux()) Skip = "OCR nativo validado no CI Linux com Tesseract e Pillow instalados.";
    }
}
