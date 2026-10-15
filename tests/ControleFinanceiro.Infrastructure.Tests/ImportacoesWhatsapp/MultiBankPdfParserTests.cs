using ControleFinanceiro.Infrastructure.ImportacoesWhatsapp;
using FluentAssertions;

namespace ControleFinanceiro.Infrastructure.Tests.ImportacoesWhatsapp;

public class MultiBankPdfParserTests
{
    [Fact]
    public void Nubank_PreservaEstornoParcelasDuplicadasEUmCentavoDeDiferenca()
    {
        var result = MultiBankPdfParser.ParseRows([
            ["Total a pagar R$ 999,00 R$ 888,00"],
            ["Nu Pagamentos S.A.", "FATURA 15 JUN 2026", "RESUMO DA FATURA ATUAL", "Fatura anterior R$ 100,00", "Pagamento recebido −R$ 100,00", "Total a pagar R$ 25,99"],
            ["TRANSAÇÕES DE 06 MAI A 06 JUN", "06 MAI •••• 1234 LOJA - Parcela 3/3 R$ 10,00", "06 MAI •••• 1234 LOJA - Parcela 3/3 R$ 10,00", "14 MAI Estorno de LOJA −R$ 4,00", "07 MAI Pagamento em 07 MAI −R$ 100,00", "06 MAI FINANCIAMENTO - Parcela 2/2 R$ 10,00", "Total a pagar: R$ 20,00 (valor total do financiamento)"]
        ])!;
        result.Itens.Select(x=>x.Valor).Should().Equal(10,10,-4,10);
        result.Itens[0].NumeroParcela.Should().Be(3);
        result.Itens[0].Descricao.Should().NotContain("1234");
        result.TotalDocumento.Should().Be(25.99m);
        result.AvisoFormato.Should().Contain("0,01");
    }

    [Fact]
    public void MercadoPago_LeSomenteConsumosEParcelaAtual()
    {
        var result = MultiBankPdfParser.ParseRows([
            ["Mercado Pago", "Total a pagar Vence em Limite total", "R$ 123,45 16/09/2026 R$ 5.000,00"],
            ["Vencimento: 16/09/2026", "Detalhes de consumo", "Data Movimentações Valor em R$", "05/09 LOJA Parcela 1 de 5 R$ 123,45", "Total R$ 123,45"],
            ["Parcele a fatura", "05/09 OFERTA R$ 500,00", "Total R$ 500,00"]
        ])!;
        result.Itens.Should().HaveCount(1);
        result.Itens[0].QuantidadeParcelas.Should().Be(5);
        result.TotalDocumento.Should().Be(123.45m);
    }

    [Fact]
    public void Bmg_UsaAnoDaCompraParceladaEIgnoraPagamento()
    {
        var result = MultiBankPdfParser.ParseRows([
            ["App Galo Bmg", "Vencimento: Limite disponível", "25/09/2026 R$ 200,00", "Saldo da fatura anterior + R$ 300,00"],
            ["Lançamentos:", "17/09 SERVICO Pc.12/18 100,00", "25/08 Pgto Fatura BMG CC -300,00", "VALOR TOTAL DA FATURA 100,00", "Pagamento dos próximos meses", "17/09 PARCELA FUTURA 400,00"]
        ])!;
        result.Itens.Should().HaveCount(1);
        result.Itens[0].DataTransacao.Should().Be(new DateOnly(2025,9,17));
        result.Itens[0].NumeroParcela.Should().Be(12);
        result.Itens[0].QuantidadeParcelas.Should().Be(18);
    }

    [Theory]
    [InlineData("05/09 LOJA Parcela 6 de 5 R$ 100,00", "100,00")]
    [InlineData("05/09 LOJA R$ 100,00", "101,00")]
    [InlineData("32/09 LOJA R$ 100,00", "100,00")]
    [InlineData("05/09 LOJA sem valor", "100,00")]
    public void BloqueiaParcelasDatasValoresOuTotaisInvalidos(string row,string total)
    {
        var result=MultiBankPdfParser.ParseRows([["Mercado Pago", "Vencimento: 16/09/2026", "Detalhes de consumo", row, "Total R$ "+total]])!;
        result.Itens.Should().BeEmpty();
    }
}
