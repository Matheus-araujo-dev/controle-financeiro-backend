using ControleFinanceiro.Application.Conciliacao;
using FluentAssertions;
namespace ControleFinanceiro.Application.Tests.Conciliacao;
public sealed class FaturaMatchingTests
{
    private static CompraComparavel Compra(decimal value, string name = "LOJA", int parcela = 2) => new(Guid.NewGuid(), new DateOnly(2026, 9, 1), name, value, parcela, 3);
    [Fact]
    public void CentavoDiferente_EncontraMasInformaAjuste()
    {
        var bank = Compra(33.33m); var account = Compra(33.34m);
        var result = FaturaMatching.Encontrar(bank, [account]);
        result.Should().ContainSingle(); result[0].Diferenca.Should().Be(-0.01m);
        result[0].CorrespondenciaClara.Should().BeTrue();
    }
    [Fact]
    public void CandidatosIguais_NaoEscolheArbitrariamente()
    {
        var result = FaturaMatching.Encontrar(Compra(33.33m), [Compra(33.33m), Compra(33.33m)]);
        result.Should().HaveCount(2); result.Should().OnlyContain(x => !x.CorrespondenciaClara);
    }
    [Fact]
    public void ParcelaOuSinalDiferentes_NaoSaoCandidatos()
    {
        FaturaMatching.Encontrar(Compra(33.33m), [Compra(-33.33m), Compra(33.33m, parcela: 3)]).Should().BeEmpty();
    }
    [Theory]
    [InlineData(33.38, 1)]
    [InlineData(33.39, 0)]
    public void ToleranciaAbsoluta_RespeitaLimite(decimal value, int expected)
    {
        FaturaMatching.Encontrar(Compra(33.33m), [Compra(value)]).Should().HaveCount(expected);
    }
    [Fact]
    public void AliasAprendido_ReconheceTituloDiferente()
    {
        FaturaMatching.Encontrar(Compra(33.33m, "ASAAS*NEXTFITago 26"), [Compra(33.34m, "Sistema do pilates")], "Sistema do pilates")
            .Single().CorrespondenciaClara.Should().BeTrue();
        EstabelecimentoKey.Normalizar("ASAAS*NEXTFITago 26").Should().Be(EstabelecimentoKey.Normalizar("ASAAS*NEXTFITset 26"));
        EstabelecimentoKey.Normalizar("ASAAS*OUTROset 26").Should().NotBe(EstabelecimentoKey.Normalizar("ASAAS*NEXTFITset 26"));
    }
    [Fact]
    public void MesmoValorSemIdentidade_DevePedirRevisao()
    {
        FaturaMatching.Encontrar(Compra(33.33m, "OUTRA LOJA"), [Compra(33.33m)]).Single().CorrespondenciaClara.Should().BeFalse();
    }
}
