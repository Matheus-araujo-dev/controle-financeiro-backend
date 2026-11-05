using ControleFinanceiro.Application.Conciliacao;
using FluentAssertions;

namespace ControleFinanceiro.Application.Tests.Conciliacao;

public sealed class DescricaoEntityMatcherTests
{
    private static readonly Guid IdTopBurguer = Guid.NewGuid();
    private static readonly Guid IdMercadoLivre = Guid.NewGuid();
    private static readonly Guid IdIFood = Guid.NewGuid();
    private static readonly Guid IdPadaria = Guid.NewGuid();
    private static readonly Guid IdFarmacia = Guid.NewGuid();

    private static readonly List<(Guid Id, string Nome)> Entidades =
    [
        (IdTopBurguer, "Top burguer açai"),
        (IdMercadoLivre, "Mercado Livre"),
        (IdIFood, "iFood"),
        (IdPadaria, "Padaria Bom Pão"),
        (IdFarmacia, "Farmácia São João")
    ];

    [Theory]
    [InlineData("TOP BURGUER ACAI")]
    [InlineData("Top Burguer Açaí")]
    [InlineData("top burguer acai")]
    [InlineData("TOP BURGUER AÇAI")]
    public void MatchExato_IgnoraAcentoECase(string descricao)
    {
        var (id, score) = DescricaoEntityMatcher.EncontrarMelhorMatch(descricao, Entidades);

        id.Should().Be(IdTopBurguer);
        score.Should().BeGreaterOrEqualTo(0.9);
    }

    [Theory]
    [InlineData("MERCADOLIVRE*MERCADOL")]
    [InlineData("MERCADO LIVRE")]
    [InlineData("Mercado Livre BR")]
    public void MatchContainment_NomeContidoNaDescricao(string descricao)
    {
        var (id, score) = DescricaoEntityMatcher.EncontrarMelhorMatch(descricao, Entidades);

        id.Should().Be(IdMercadoLivre);
        score.Should().BeGreaterOrEqualTo(0.7);
    }

    [Theory]
    [InlineData("PAG*TOPBURGUERACAI")]
    [InlineData("PAGTO*TOP BURGUER ACAI")]
    [InlineData("MERCPAGO*TOP BURGUER ACAI")]
    public void MatchComPrefixoGateway_RemovePrefixo(string descricao)
    {
        var (id, score) = DescricaoEntityMatcher.EncontrarMelhorMatch(descricao, Entidades);

        id.Should().Be(IdTopBurguer);
        score.Should().BeGreaterOrEqualTo(0.7);
    }

    [Theory]
    [InlineData("PADARIA BOM PAO")]
    [InlineData("Padaria Bom Pão - Centro")]
    public void MatchTokens_PadariaComTokensCompletos(string descricao)
    {
        var (id, score) = DescricaoEntityMatcher.EncontrarMelhorMatch(descricao, Entidades);

        id.Should().Be(IdPadaria);
        score.Should().BeGreaterOrEqualTo(0.7);
    }

    [Theory]
    [InlineData("FARMACIA SAO JOAO")]
    [InlineData("FARMACIA SÃO JOÃO")]
    public void MatchAcentuacao_FarmaciaComCaracteresEspeciais(string descricao)
    {
        var (id, score) = DescricaoEntityMatcher.EncontrarMelhorMatch(descricao, Entidades);

        id.Should().Be(IdFarmacia);
        score.Should().BeGreaterOrEqualTo(0.9);
    }

    [Fact]
    public void SemMatch_RetornaNull()
    {
        var (id, score) = DescricaoEntityMatcher.EncontrarMelhorMatch("LOJA DESCONHECIDA XYZ", Entidades);

        id.Should().BeNull();
        score.Should().BeLessThan(0.5);
    }

    [Fact]
    public void DescricaoVazia_RetornaNull()
    {
        var (id, score) = DescricaoEntityMatcher.EncontrarMelhorMatch("", Entidades);

        id.Should().BeNull();
        score.Should().Be(0);
    }

    [Fact]
    public void ListaVazia_RetornaNull()
    {
        var (id, score) = DescricaoEntityMatcher.EncontrarMelhorMatch("TOP BURGUER ACAI", []);

        id.Should().BeNull();
        score.Should().Be(0);
    }

    [Fact]
    public void EnriquecerComHeuristica_PreencheRecebedorFaltante()
    {
        var itemId = Guid.NewGuid();
        var llmResult = new SugestoesConciliacaoResponse([
            new SugestaoItemConciliacaoResponse(itemId, "Top Burguer", null, null, Guid.NewGuid(), 0.7)
        ]);
        var heuristicMatches = new Dictionary<Guid, (Guid? Id, double Score)>
        {
            [itemId] = (IdTopBurguer, 0.95)
        };

        var result = ConciliacaoSugestaoService.EnriquecerComHeuristica(llmResult, heuristicMatches);

        result.Sugestoes[0].RecebedorId.Should().Be(IdTopBurguer);
        result.Sugestoes[0].Confianca.Should().Be(0.95);
        result.Sugestoes[0].ContaGerencialId.Should().NotBeNull();
    }

    [Fact]
    public void EnriquecerComHeuristica_NaoSobrescreveRecebedorExistente()
    {
        var itemId = Guid.NewGuid();
        var outroRecebedor = Guid.NewGuid();
        var llmResult = new SugestoesConciliacaoResponse([
            new SugestaoItemConciliacaoResponse(itemId, "Top Burguer", outroRecebedor, null, null, 0.8)
        ]);
        var heuristicMatches = new Dictionary<Guid, (Guid? Id, double Score)>
        {
            [itemId] = (IdTopBurguer, 0.95)
        };

        var result = ConciliacaoSugestaoService.EnriquecerComHeuristica(llmResult, heuristicMatches);

        result.Sugestoes[0].RecebedorId.Should().Be(outroRecebedor);
    }

    [Fact]
    public void EnriquecerComHeuristica_IgnoraScoreBaixo()
    {
        var itemId = Guid.NewGuid();
        var llmResult = new SugestoesConciliacaoResponse([
            new SugestaoItemConciliacaoResponse(itemId, "Algo", null, null, null, 0.5)
        ]);
        var heuristicMatches = new Dictionary<Guid, (Guid? Id, double Score)>
        {
            [itemId] = (IdTopBurguer, 0.4)
        };

        var result = ConciliacaoSugestaoService.EnriquecerComHeuristica(llmResult, heuristicMatches);

        result.Sugestoes[0].RecebedorId.Should().BeNull();
    }

    [Fact]
    public void MatchComSufixoParcela_IgnoraSufixo()
    {
        var (id, score) = DescricaoEntityMatcher.EncontrarMelhorMatch("TOP BURGUER ACAI 2/3", Entidades);

        id.Should().Be(IdTopBurguer);
        score.Should().BeGreaterOrEqualTo(0.9);
    }

    [Fact]
    public void MatchComSufixoMes_IgnoraSufixo()
    {
        var (id, score) = DescricaoEntityMatcher.EncontrarMelhorMatch("FARMACIA SAO JOAO SET24", Entidades);

        id.Should().Be(IdFarmacia);
        score.Should().BeGreaterOrEqualTo(0.9);
    }
}
