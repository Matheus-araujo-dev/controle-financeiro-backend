using ControleFinanceiro.Domain.Conciliacao;
using FluentAssertions;
using System.Text.Json;

namespace ControleFinanceiro.Domain.Tests.Conciliacao;

public sealed class MemoriaEstabelecimentoTests
{
    [Fact]
    public void DecisaoParcial_PreservaOsOutrosCamposAprendidos()
    {
        var memoria = MemoriaEstabelecimento.Criar(Guid.NewGuid(), "ASAAS NEXTFIT");
        memoria.AplicarDecisao(new Dictionary<string, string> { ["descricao"] = "\"Sistema do pilates\"", ["gerarReembolso"] = "true" });
        memoria.AplicarDecisao(new Dictionary<string, string> { ["descricao"] = "\"Software do pilates\"" });
        using var json = JsonDocument.Parse(memoria.PreferenciasJson);
        json.RootElement.GetProperty("descricao").GetString().Should().Be("Software do pilates");
        json.RootElement.GetProperty("gerarReembolso").GetBoolean().Should().BeTrue();
        memoria.Confirmacoes.Should().Be(2);
    }

    [Fact]
    public void DecisaoInvalida_NaoAlteraCamposJaAprendidos()
    {
        var memoria = MemoriaEstabelecimento.Criar(Guid.NewGuid(), "LOJA");
        memoria.AplicarDecisao(new Dictionary<string, string> { ["descricao"] = "\"Compra\"" });
        var anterior = memoria.PreferenciasJson;
        var action = () => memoria.AplicarDecisao(new Dictionary<string, string> { ["descricao"] = "\"Alterado\"", ["responsavelCompraId"] = "json inválido" });
        action.Should().Throw<ArgumentException>();
        memoria.PreferenciasJson.Should().Be(anterior);
        memoria.Confirmacoes.Should().Be(1);
    }

    [Fact]
    public void Criar_ExigeContextoDoCartao()
    {
        var action = () => MemoriaEstabelecimento.Criar(Guid.Empty, "LOJA");
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Criar_ChaveVazia_LancaExcecao()
    {
        var act = () => MemoriaEstabelecimento.Criar(Guid.NewGuid(), "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Criar_ChaveExcede500_LancaExcecao()
    {
        var act = () => MemoriaEstabelecimento.Criar(Guid.NewGuid(), new string('X', 501));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Criar_ChaveValida_CriaMemoria()
    {
        var cartaoId = Guid.NewGuid();
        var memoria = MemoriaEstabelecimento.Criar(cartaoId, "MERCADO LIVRE");
        memoria.CartaoId.Should().Be(cartaoId);
        memoria.Chave.Should().Be("MERCADO LIVRE");
        memoria.PreferenciasJson.Should().Be("{}");
        memoria.Confirmacoes.Should().Be(0);
    }

    [Fact]
    public void AplicarDecisao_CamposVazios_LancaExcecao()
    {
        var memoria = MemoriaEstabelecimento.Criar(Guid.NewGuid(), "LOJA");
        var act = () => memoria.AplicarDecisao(new Dictionary<string, string>());
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AplicarDecisao_CampoExcede80Caracteres_LancaExcecao()
    {
        var memoria = MemoriaEstabelecimento.Criar(Guid.NewGuid(), "LOJA");
        var campoGrande = new string('x', 81);
        var act = () => memoria.AplicarDecisao(new Dictionary<string, string> { [campoGrande] = "\"valor\"" });
        act.Should().Throw<ArgumentException>();
    }
}
