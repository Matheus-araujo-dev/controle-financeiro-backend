using ControleFinanceiro.Application.Financeiro.Importacao;
using FluentAssertions;

namespace ControleFinanceiro.Application.Tests.Financeiro.Importacao;

public sealed class OfxFaturaParserTests
{
    [Fact]
    public void Parse_ComBlocosStmtTrn_DeveExtrairDebitos()
    {
        var ofx = """
            <OFX><BANKTRANLIST>
            <STMTTRN><TRNTYPE>DEBIT<DTPOSTED>20260601120000[-03:00]<TRNAMT>-150.50<MEMO>Mercado Central</STMTTRN>
            <STMTTRN><TRNTYPE>CREDIT<DTPOSTED>20260602<TRNAMT>200.00<MEMO>Pagamento recebido</STMTTRN>
            <STMTTRN><TRNTYPE>POS<DTPOSTED>20260603<TRNAMT>-33.00<NAME>Posto Shell</STMTTRN>
            </BANKTRANLIST></OFX>
            """;

        var resultado = OfxFaturaParser.Parse(ofx);

        resultado.AvisoFormato.Should().BeNull();
        resultado.Itens.Should().HaveCount(2); // crédito ignorado
        resultado.Itens[0].Descricao.Should().Be("Mercado Central");
        resultado.Itens[0].Valor.Should().Be(150.50m); // valor absoluto
        resultado.Itens[0].DataTransacao.Should().Be(new DateOnly(2026, 6, 1));
        resultado.Itens[1].Descricao.Should().Be("Posto Shell"); // usa NAME quando não há MEMO
    }

    [Fact]
    public void Parse_SgmlSemFechamento_DeveUsarFallbackSequencial()
    {
        var ofx = string.Join('\n',
            "OFXHEADER:100",
            "<STMTTRN><TRNTYPE>DEBIT<DTPOSTED>20260601<TRNAMT>-10.00<MEMO>Cafe",
            "<STMTTRN><TRNTYPE>DEBIT<DTPOSTED>20260602<TRNAMT>-20.00<MEMO>Almoco");

        var resultado = OfxFaturaParser.Parse(ofx);

        resultado.Itens.Should().HaveCount(2);
        resultado.Itens.Select(i => i.Descricao).Should().Contain(["Cafe", "Almoco"]);
    }

    [Fact]
    public void Parse_Vazio_DeveRetornarAviso()
    {
        var resultado = OfxFaturaParser.Parse("   ");

        resultado.Itens.Should().BeEmpty();
        resultado.AvisoFormato.Should().Be("Arquivo OFX vazio.");
    }

    [Fact]
    public void Parse_SemDebitos_DeveRetornarAviso()
    {
        var ofx = "<STMTTRN><TRNTYPE>CREDIT<DTPOSTED>20260601<TRNAMT>50.00<MEMO>Estorno</STMTTRN>";

        var resultado = OfxFaturaParser.Parse(ofx);

        resultado.Itens.Should().BeEmpty();
        resultado.AvisoFormato.Should().Contain("Nenhuma transação");
    }
}
