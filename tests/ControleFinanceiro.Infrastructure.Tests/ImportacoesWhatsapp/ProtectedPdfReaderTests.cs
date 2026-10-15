using ControleFinanceiro.Infrastructure.ImportacoesWhatsapp;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace ControleFinanceiro.Infrastructure.Tests.ImportacoesWhatsapp;

public class ProtectedPdfReaderTests
{
    private static Stream Pdf() => File.OpenRead(Path.Combine(AppContext.BaseDirectory,"ImportacoesWhatsapp","Fixtures","fatura-protegida-sintetica.pdf"));
    private static PythonPdfTextReader Reader() => new(Options.Create(new InvoiceOcrOptions {
        PythonExecutable = Environment.GetEnvironmentVariable("INVOICE_TEST_PYTHON") ?? "python3"
    }));

    [PythonPdfFact]
    public async Task RuntimeReal_DescriptografaELeSomenteAParcelaAtual()
    {
        using var pdf = Pdf();
        var text = await Reader().ReadAsync(pdf,"teste-pdf",default);
        text.Error.Should().BeNull();
        var parsed = MultiBankPdfParser.ParseRows(text.Pages)!;
        parsed.Itens.Should().ContainSingle().Which.Valor.Should().Be(100);
        parsed.Itens[0].NumeroParcela.Should().Be(12);
        pdf.Position=0;
        var routed=await new BradescoPdfFaturaReader(textReader:Reader()).ParseAsync(pdf,default,null,"teste-pdf");
        routed.Itens.Should().ContainSingle().Which.QuantidadeParcelas.Should().Be(18);
    }

    [PythonPdfFact]
    public async Task SenhaAusenteOuIncorreta_NaoRetornaLancamentos()
    {
        foreach(var password in new string?[] {null,"incorreta"})
        {
            using var pdf=Pdf();
            var parsed=await new BradescoPdfFaturaReader(textReader:Reader()).ParseAsync(pdf,default,null,password);
            parsed.Itens.Should().BeEmpty();
            parsed.AvisoFormato.Should().Contain("senha correta");
        }
    }
}

public sealed class PythonPdfFactAttribute : FactAttribute
{
    public PythonPdfFactAttribute()
    {
        if (!OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("INVOICE_TEST_PYTHON") is null)
            Skip="Runtime Python validado no CI Linux; configure INVOICE_TEST_PYTHON para executar localmente.";
    }
}
