using ControleFinanceiro.Infrastructure.ImportacoesWhatsapp;
using FluentAssertions;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace ControleFinanceiro.Infrastructure.Tests.ImportacoesWhatsapp;

public sealed class BradescoMensalPdfParserTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LayoutMensal_PreservaParcelasCreditosEComprasIguais(bool sinalSeparado)
    {
        using var input = new MemoryStream(Pdf(sinalSeparado));
        var result = await new BradescoPdfFaturaReader().ParseAsync(input, default);
        result.Itens.Should().HaveCount(6, result.AvisoFormato);
        result.Itens.Sum(i => i.Valor).Should().Be(323.39m);
        result.Itens.Should().Contain(i => i.Descricao == "LOJA07/12" && i.NumeroParcela == 7 && i.QuantidadeParcelas == 12);
        result.Itens.Should().Contain(i => i.Valor == -20m);
        result.Itens.Count(i => i.Descricao == "COMPRA IGUAL").Should().Be(2);
        result.Itens.Should().OnlyContain(i => i.DataVencimentoFatura == new DateOnly(2026, 9, 20));
        result.Itens.Should().NotContain(i => i.Descricao.Contains("TITULAR") || i.Descricao.Contains("PAGTO"));
    }

    [Fact]
    public async Task TotalDivergente_NaoLiberaImportacaoParcial()
    {
        using var input = new MemoryStream(Pdf(true, "999,99"));
        var result = await new BradescoPdfFaturaReader().ParseAsync(input, default);
        result.Itens.Should().BeEmpty();
        result.AvisoFormato.Should().Contain("Conferência divergente");
    }

    private static byte[] Pdf(bool separate, string total = "323,39")
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var cover = builder.AddPage(PageSize.A4);
        void Put(PdfPageBuilder p, string text, double x, double y) => p.AddText(text, 6, new PdfPoint(x,y), font);
        Put(cover,"Bradesco Fatura Mensal",45,800);
        Put(cover,"Vencimento",495,750); Put(cover,"20/09/2026",495,733); Put(cover,total,430,733);
        Put(cover,"Saldo anterior",45,650); Put(cover,"1.000,00",320,650);
        var page = builder.AddPage(PageSize.A4);
        Put(page,"Data",45,700); Put(page,"Historico",66,700); Put(page,"Cidade",204,700); Put(page,"US$",253,700); Put(page,"R$",332,700);
        void Row(string date, string desc, string value, double y, bool negative = false)
        {
            Put(page,date,45,y);Put(page,desc,66,y);Put(page,"CIDADE",205,y);
            Put(page,value + (negative && !separate ? "-" : ""),320,y);
            if (negative && separate) Put(page,"-",320+value.Length*3.4,y);
        }
        Row("20/08","PAGTO. POR DEB EM C/C","1.000,00",680,true);
        Row("05/09","MERCADO","150,00",660);
        Put(page,"TITULAR EXEMPLO",45,651);Put(page,"4005 XXXX XXXX 1111",209,651);
        Row("12/12","LOJA07/1","100,00",640);Put(page,"2",66,633);
        Row("08/09","ESTORNO","20,00",620,true);
        Row("09/09","COMPRA IGUAL","30,00",600);Row("09/09","COMPRA IGUAL","30,00",590);
        Put(page,"Taxas 99,99 Limites 999,99",365,660);
        var next = builder.AddPage(PageSize.A4);
        Put(next,"01/09",45,780); Put(next,"SERVICO USD 6,15",66,780);Put(next,"CIDADE",205,780);
        Put(next,"6,15",253,780);Put(next,"5,4300",280,780);Put(next,"33,39",320,780);
        return builder.Build();
    }
}
