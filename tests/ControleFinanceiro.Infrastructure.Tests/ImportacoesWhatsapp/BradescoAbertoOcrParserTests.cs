using ControleFinanceiro.Infrastructure.ImportacoesWhatsapp;
using FluentAssertions;

namespace ControleFinanceiro.Infrastructure.Tests.ImportacoesWhatsapp;

public class BradescoAbertoOcrParserTests
{
    private const string Header = "Aplicativo Bradesco\nData: 25/09/2026 - 16:50\nSituação do Extrato: Em Aberto\n";
    private static readonly DateOnly Due = new(2026, 10, 20);

    [Fact]
    public void PreservaRepeticoesEstornoViradaAnoEIgnoraPagamentoSaldo()
    {
        var parsed = BradescoAbertoOcrParser.Parse(Header + """
            21/09 PAGTO. POR DEB EM C/C USD 0,00 R$ 0,00 R$
            -100,00
            20/09 SALDO ANTERIOR USD 0,00 R$ 0,00 R$ 100,00
            18/12 LOJA USD 0,00 R$ 0,00 R$ 30,00
            18/12 LOJA USD 0,00 R$ 0,00 R$ 30,00
            11/09 ESTORNO LOJA USD 0,00 R$ 0,00 R$ -10,00
            Total para: PESSOA R$ 50,00
            Total para: CARTAO VAZIO
            23/09 OUTRO CARTAO USD 0,00 R$ 0,00 R$ 20,00
            Total para: PESSOA R$ 20,00
            """, Due);
        parsed.Itens.Select(x => x.Valor).Should().Equal(30, 30, -10, 20);
        parsed.Itens[0].DataTransacao.Should().Be(new DateOnly(2025, 12, 18));
        parsed.Itens.Should().OnlyContain(x => x.DataVencimentoFatura == Due);
    }

    [Theory]
    [InlineData("22/09 LOJA USD 0,00 R$ 0,00 R$ 10,00\nTotal para: PESSOA R$ 11,00")]
    [InlineData("22/09 LOJA USD 0,00 R$ 0,00 R$ ilegivel\nTotal para: PESSOA R$ 10,00")]
    [InlineData("32/09 LOJA USD 0,00 R$ 0,00 R$ 10,00\nTotal para: PESSOA R$ 10,00")]
    [InlineData("22/09 LOJA USD 0,00 R$ 0,00 R$ 10,00")]
    public void BloqueiaLeituraIncompletaOuTotalDivergente(string text)
        => BradescoAbertoOcrParser.Parse(Header + text, Due).Itens.Should().BeEmpty();

    [Fact]
    public void RecuperaSeparadoresOcrSomenteComConferenciaDoSubtotal()
    {
        var parsed = BradescoAbertoOcrParser.Parse(Header + "2110 LOJA USD 0,00 R$ 0,00 R$ 71/18\n07/10 LOJA USD 0,00 R$ 0,00 R$ 999\nTotal para: PESSOA R$ 81,17", Due);
        parsed.Itens.Select(x => x.Valor).Should().Equal(71.18m, 9.99m);
        parsed.Itens[0].DataTransacao.Should().Be(new DateOnly(2025, 10, 21));
    }

    [Fact]
    public void ExigeFaturaSelecionadaENaoAceitaOutroBanco()
    {
        BradescoAbertoOcrParser.Parse(Header, null).Itens.Should().BeEmpty();
        BradescoAbertoOcrParser.Parse(Header.Replace("Bradesco", "Outro"), Due).Itens.Should().BeEmpty();
    }
}
