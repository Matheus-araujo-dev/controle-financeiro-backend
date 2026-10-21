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
}
