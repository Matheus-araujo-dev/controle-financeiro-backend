using ControleFinanceiro.Application.Conciliacao;
using ControleFinanceiro.Domain.Conciliacao;
using FluentAssertions;

namespace ControleFinanceiro.Application.Tests.Conciliacao;

public sealed class ConciliacaoSugestaoParseTests
{
    private static readonly Guid Cat1 = Guid.NewGuid();
    private static readonly Guid Cat2 = Guid.NewGuid();
    private static readonly Guid Rec1 = Guid.NewGuid();
    private static readonly Guid Resp1 = Guid.NewGuid();

    private static readonly IReadOnlySet<Guid> Categorias = new HashSet<Guid> { Cat1, Cat2 };
    private static readonly IReadOnlySet<Guid> Recebedores = new HashSet<Guid> { Rec1 };
    private static readonly IReadOnlySet<Guid> Responsaveis = new HashSet<Guid> { Resp1 };

    private static ItemConciliacao CriarItem(string descricao = "COMPRA TESTE", decimal valor = 100m)
        => ItemConciliacao.CriarFatura(new DateOnly(2026, 10, 1), descricao, valor, $"KEY-{Guid.NewGuid()}", 1, 1);

    [Fact]
    public void JsonNulo_RetornaSugestoesPadrao()
    {
        var itens = new[] { CriarItem() };

        var result = ConciliacaoSugestaoService.ParseSugestoes(null, itens, Categorias, Recebedores, Responsaveis);

        result.Sugestoes.Should().HaveCount(1);
        result.Sugestoes[0].ItemId.Should().Be(itens[0].Id);
        result.Sugestoes[0].Confianca.Should().Be(0);
        result.Sugestoes[0].Descricao.Should().BeNull();
    }

    [Fact]
    public void JsonVazio_RetornaSugestoesPadrao()
    {
        var itens = new[] { CriarItem() };

        var result = ConciliacaoSugestaoService.ParseSugestoes("  ", itens, Categorias, Recebedores, Responsaveis);

        result.Sugestoes.Should().HaveCount(1);
        result.Sugestoes[0].Confianca.Should().Be(0);
    }

    [Fact]
    public void JsonValido_RetornaSugestoesCorretas()
    {
        var item = CriarItem("PAG*JoseDaSilva");
        var json = $$"""
            {"sugestoes":[{"itemId":"{{item.Id}}","descricao":"José da Silva","recebedorId":"{{Rec1}}","responsavelCompraId":"{{Resp1}}","contaGerencialId":"{{Cat1}}","confianca":0.9}]}
            """;

        var result = ConciliacaoSugestaoService.ParseSugestoes(json, [item], Categorias, Recebedores, Responsaveis);

        result.Sugestoes.Should().HaveCount(1);
        var s = result.Sugestoes[0];
        s.ItemId.Should().Be(item.Id);
        s.Descricao.Should().Be("José da Silva");
        s.RecebedorId.Should().Be(Rec1);
        s.ResponsavelCompraId.Should().Be(Resp1);
        s.ContaGerencialId.Should().Be(Cat1);
        s.Confianca.Should().Be(0.9);
    }

    [Fact]
    public void IdInvalido_EhNulificado()
    {
        var item = CriarItem();
        var idFalso = Guid.NewGuid();
        var json = $$"""
            {"sugestoes":[{"itemId":"{{item.Id}}","descricao":"Teste","recebedorId":"{{idFalso}}","responsavelCompraId":"{{idFalso}}","contaGerencialId":"{{idFalso}}","confianca":0.5}]}
            """;

        var result = ConciliacaoSugestaoService.ParseSugestoes(json, [item], Categorias, Recebedores, Responsaveis);

        var s = result.Sugestoes[0];
        s.RecebedorId.Should().BeNull();
        s.ResponsavelCompraId.Should().BeNull();
        s.ContaGerencialId.Should().BeNull();
    }

    [Fact]
    public void MarkdownFences_SaoRemovidas()
    {
        var item = CriarItem();
        var json = $"""
            ```json
            {{"sugestoes":[{{"itemId":"{item.Id}","descricao":"Limpo","recebedorId":null,"responsavelCompraId":null,"contaGerencialId":null,"confianca":0.7}}]}}
            ```
            """;

        var result = ConciliacaoSugestaoService.ParseSugestoes(json, [item], Categorias, Recebedores, Responsaveis);

        result.Sugestoes.Should().HaveCount(1);
        result.Sugestoes[0].Descricao.Should().Be("Limpo");
    }

    [Fact]
    public void ItensFaltantes_RecebesSugestoesPadrao()
    {
        var item1 = CriarItem("COMPRA A");
        var item2 = CriarItem("COMPRA B");
        var json = $$"""
            {"sugestoes":[{"itemId":"{{item1.Id}}","descricao":"Compra A","recebedorId":null,"responsavelCompraId":null,"contaGerencialId":null,"confianca":0.8}]}
            """;

        var result = ConciliacaoSugestaoService.ParseSugestoes(json, [item1, item2], Categorias, Recebedores, Responsaveis);

        result.Sugestoes.Should().HaveCount(2);
        var missing = result.Sugestoes.Single(s => s.ItemId == item2.Id);
        missing.Confianca.Should().Be(0);
        missing.Descricao.Should().BeNull();
    }

    [Fact]
    public void DescricaoLonga_Truncada200Chars()
    {
        var item = CriarItem();
        var descLonga = new string('A', 300);
        var json = $$"""
            {"sugestoes":[{"itemId":"{{item.Id}}","descricao":"{{descLonga}}","recebedorId":null,"responsavelCompraId":null,"contaGerencialId":null,"confianca":0.5}]}
            """;

        var result = ConciliacaoSugestaoService.ParseSugestoes(json, [item], Categorias, Recebedores, Responsaveis);

        result.Sugestoes[0].Descricao.Should().HaveLength(200);
    }

    [Fact]
    public void JsonInvalido_RetornaSugestoesPadrao()
    {
        var item = CriarItem();

        var result = ConciliacaoSugestaoService.ParseSugestoes("não é json {{{", [item], Categorias, Recebedores, Responsaveis);

        result.Sugestoes.Should().HaveCount(1);
        result.Sugestoes[0].Confianca.Should().Be(0);
    }

    [Fact]
    public void ItemIdDesconhecido_EhIgnorado()
    {
        var item = CriarItem();
        var json = $$"""
            {"sugestoes":[{"itemId":"{{Guid.NewGuid()}}","descricao":"Intruso","recebedorId":null,"responsavelCompraId":null,"contaGerencialId":null,"confianca":1.0},{"itemId":"{{item.Id}}","descricao":"Real","recebedorId":null,"responsavelCompraId":null,"contaGerencialId":null,"confianca":0.6}]}
            """;

        var result = ConciliacaoSugestaoService.ParseSugestoes(json, [item], Categorias, Recebedores, Responsaveis);

        result.Sugestoes.Should().HaveCount(1);
        result.Sugestoes[0].Descricao.Should().Be("Real");
    }
}
