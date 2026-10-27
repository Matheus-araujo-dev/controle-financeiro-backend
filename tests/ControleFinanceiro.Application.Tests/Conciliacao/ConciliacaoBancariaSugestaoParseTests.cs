using ControleFinanceiro.Application.Conciliacao;
using ControleFinanceiro.Domain.Conciliacao;
using ControleFinanceiro.Domain.Financeiro;
using FluentAssertions;

namespace ControleFinanceiro.Application.Tests.Conciliacao;

public sealed class ConciliacaoBancariaSugestaoParseTests
{
    private static ItemConciliacao CriarItem(string descricao = "PIX JOSE SILVA", decimal valor = 100m)
        => ItemConciliacao.Criar(new DateOnly(2026, 10, 1), descricao, valor, null);

    private static MovimentacaoFinanceira CriarMov(decimal valor = 100m, string? obs = "José da Silva")
        => MovimentacaoFinanceira.CriarLiquidacaoContaReceber(
            Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2026, 10, 1), valor,
            StatusMovimentacao.EfetivadaId, obs);

    [Fact]
    public void JsonNulo_NaoAlteraItens()
    {
        var item = CriarItem();
        var mov = CriarMov();
        var movMap = new Dictionary<Guid, MovimentacaoFinanceira> { [mov.Id] = mov };

        ConciliacaoBancariaSugestaoService.ApplyMatches(null, [item], movMap);

        item.SugestaoMovimentacaoId.Should().BeNull();
    }

    [Fact]
    public void JsonVazio_NaoAlteraItens()
    {
        var item = CriarItem();
        var mov = CriarMov();
        var movMap = new Dictionary<Guid, MovimentacaoFinanceira> { [mov.Id] = mov };

        ConciliacaoBancariaSugestaoService.ApplyMatches("  ", [item], movMap);

        item.SugestaoMovimentacaoId.Should().BeNull();
    }

    [Fact]
    public void MatchValido_AtualizaSugestao()
    {
        var item = CriarItem();
        var mov = CriarMov();
        var movMap = new Dictionary<Guid, MovimentacaoFinanceira> { [mov.Id] = mov };
        var json = $$"""{"matches":[{"itemId":"{{item.Id}}","movimentacaoId":"{{mov.Id}}","confianca":0.9}]}""";

        ConciliacaoBancariaSugestaoService.ApplyMatches(json, [item], movMap);

        item.SugestaoMovimentacaoId.Should().Be(mov.Id);
        item.ScoreSugestao.Should().Be(0.9m);
    }

    [Fact]
    public void ConfiancaBaixa_NaoAplica()
    {
        var item = CriarItem();
        var mov = CriarMov();
        var movMap = new Dictionary<Guid, MovimentacaoFinanceira> { [mov.Id] = mov };
        var json = $$"""{"matches":[{"itemId":"{{item.Id}}","movimentacaoId":"{{mov.Id}}","confianca":0.3}]}""";

        ConciliacaoBancariaSugestaoService.ApplyMatches(json, [item], movMap);

        item.SugestaoMovimentacaoId.Should().BeNull();
    }

    [Fact]
    public void MovimentacaoInvalida_Ignorada()
    {
        var item = CriarItem();
        var mov = CriarMov();
        var movMap = new Dictionary<Guid, MovimentacaoFinanceira> { [mov.Id] = mov };
        var json = $$"""{"matches":[{"itemId":"{{item.Id}}","movimentacaoId":"{{Guid.NewGuid()}}","confianca":0.9}]}""";

        ConciliacaoBancariaSugestaoService.ApplyMatches(json, [item], movMap);

        item.SugestaoMovimentacaoId.Should().BeNull();
    }

    [Fact]
    public void ScoreExistenteMaior_NaoSobrescreve()
    {
        var item = CriarItem();
        var mov1 = CriarMov();
        var mov2 = CriarMov(obs: "Outro");
        item.DefinirSugestao(mov1.Id, 0.95m);
        var movMap = new Dictionary<Guid, MovimentacaoFinanceira> { [mov2.Id] = mov2 };
        var json = $$"""{"matches":[{"itemId":"{{item.Id}}","movimentacaoId":"{{mov2.Id}}","confianca":0.8}]}""";

        ConciliacaoBancariaSugestaoService.ApplyMatches(json, [item], movMap);

        item.SugestaoMovimentacaoId.Should().Be(mov1.Id);
        item.ScoreSugestao.Should().Be(0.95m);
    }

    [Fact]
    public void MovimentacaoDuplicada_ApenasUmItem()
    {
        var item1 = CriarItem("PIX A");
        var item2 = CriarItem("PIX B");
        var mov = CriarMov();
        var movMap = new Dictionary<Guid, MovimentacaoFinanceira> { [mov.Id] = mov };
        var json = $$"""{"matches":[{"itemId":"{{item1.Id}}","movimentacaoId":"{{mov.Id}}","confianca":0.9},{"itemId":"{{item2.Id}}","movimentacaoId":"{{mov.Id}}","confianca":0.8}]}""";

        ConciliacaoBancariaSugestaoService.ApplyMatches(json, [item1, item2], movMap);

        item1.SugestaoMovimentacaoId.Should().Be(mov.Id);
        item2.SugestaoMovimentacaoId.Should().BeNull();
    }

    [Fact]
    public void MarkdownFences_SaoRemovidas()
    {
        var item = CriarItem();
        var mov = CriarMov();
        var movMap = new Dictionary<Guid, MovimentacaoFinanceira> { [mov.Id] = mov };
        var json = $$"""
            ```json
            {"matches":[{"itemId":"{{item.Id}}","movimentacaoId":"{{mov.Id}}","confianca":0.85}]}
            ```
            """;

        ConciliacaoBancariaSugestaoService.ApplyMatches(json, [item], movMap);

        item.SugestaoMovimentacaoId.Should().Be(mov.Id);
    }

    [Fact]
    public void JsonInvalido_NaoAlteraItens()
    {
        var item = CriarItem();
        var mov = CriarMov();
        var movMap = new Dictionary<Guid, MovimentacaoFinanceira> { [mov.Id] = mov };

        ConciliacaoBancariaSugestaoService.ApplyMatches("não é json {{{", [item], movMap);

        item.SugestaoMovimentacaoId.Should().BeNull();
    }

    [Fact]
    public void ScoreLlmMaiorQueExistente_Sobrescreve()
    {
        var item = CriarItem();
        var mov1 = CriarMov();
        var mov2 = CriarMov(obs: "Melhor");
        item.DefinirSugestao(mov1.Id, 0.6m);
        var movMap = new Dictionary<Guid, MovimentacaoFinanceira> { [mov2.Id] = mov2 };
        var json = $$"""{"matches":[{"itemId":"{{item.Id}}","movimentacaoId":"{{mov2.Id}}","confianca":0.9}]}""";

        ConciliacaoBancariaSugestaoService.ApplyMatches(json, [item], movMap);

        item.SugestaoMovimentacaoId.Should().Be(mov2.Id);
        item.ScoreSugestao.Should().Be(0.9m);
    }
}
