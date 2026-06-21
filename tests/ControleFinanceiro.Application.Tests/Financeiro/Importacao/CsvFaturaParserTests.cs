using ControleFinanceiro.Application.Financeiro.Importacao;
using FluentAssertions;

namespace ControleFinanceiro.Application.Tests.Financeiro.Importacao;

public sealed class CsvFaturaParserTests
{
    [Fact]
    public void Parse_ComCabecalhoNomeado_DeveExtrairItens()
    {
        var csv = string.Join('\n',
            "data,descricao,valor",
            "2026-06-01,Mercado,\"150,50\"",
            "2026-06-02,Farmacia,\"1.200,00\"");

        var resultado = CsvFaturaParser.Parse(csv);

        resultado.AvisoFormato.Should().BeNull();
        resultado.Itens.Should().HaveCount(2);
        resultado.Itens[0].Descricao.Should().Be("Mercado");
        resultado.Itens[0].Valor.Should().Be(150.50m);
        resultado.Itens[1].Valor.Should().Be(1200.00m);
    }

    [Fact]
    public void Parse_ComSeparadorPontoEVirgula_DeveDetectar()
    {
        var csv = "Data;Estabelecimento;Valor\n01/06/2026;Posto;\"99,90\"";

        var resultado = CsvFaturaParser.Parse(csv);

        resultado.Itens.Should().ContainSingle();
        resultado.Itens[0].Descricao.Should().Be("Posto");
        resultado.Itens[0].Valor.Should().Be(99.90m);
    }

    [Fact]
    public void Parse_DeveIgnorarCreditosEValoresInvalidos()
    {
        var csv = string.Join('\n',
            "data,descricao,valor",
            "2026-06-01,Compra,100.00",
            "2026-06-02,Estorno,-30.00",
            "data-invalida,Lixo,10.00",
            "2026-06-03,SemValor,abc");

        var resultado = CsvFaturaParser.Parse(csv);

        resultado.Itens.Should().ContainSingle(i => i.Descricao == "Compra");
    }

    [Fact]
    public void Parse_SemCabecalhoConhecido_DeveUsarPosicaoEAvisar()
    {
        var csv = "col1,col2,col3\n2026-06-01,Padaria,12.00";

        var resultado = CsvFaturaParser.Parse(csv);

        resultado.AvisoFormato.Should().NotBeNull();
        resultado.Itens.Should().ContainSingle(i => i.Descricao == "Padaria");
    }

    [Fact]
    public void Parse_ComMenosDeDuasLinhas_DeveRetornarAviso()
    {
        var resultado = CsvFaturaParser.Parse("apenas-cabecalho");

        resultado.Itens.Should().BeEmpty();
        resultado.AvisoFormato.Should().NotBeNull();
    }

    [Fact]
    public void Parse_ComMenosDeTresColunasSemCabecalho_DeveFalhar()
    {
        var csv = "a,b\nx,y";

        var resultado = CsvFaturaParser.Parse(csv);

        resultado.Itens.Should().BeEmpty();
        resultado.AvisoFormato.Should().Contain("colunas");
    }

    [Fact]
    public void GerarChave_DeveSerDeterministicaE16Hex()
    {
        var item = new CsvFaturaItem(new DateOnly(2026, 6, 1), "Mercado", 150.50m);

        var chave1 = CsvFaturaParser.GerarChave(item);
        var chave2 = CsvFaturaParser.GerarChave(item);

        chave1.Should().Be(chave2);
        chave1.Should().MatchRegex("^[0-9A-F]{16}$");
    }
}
