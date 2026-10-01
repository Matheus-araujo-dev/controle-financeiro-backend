using ControleFinanceiro.Application.Dashboard;
using FluentAssertions;

namespace ControleFinanceiro.Application.Tests.Dashboard;

public sealed class AnomaliasDetectorTests
{
    private static readonly DateOnly Mes = new(2027, 4, 1);
    private static readonly Guid Recebedor = Guid.NewGuid();
    private static AnomaliaConta Conta(int mes, decimal valor = 100m, Guid? regra = null) =>
        new(Guid.NewGuid(), new DateOnly(2027, mes, 5), "Internet", Recebedor, null, null, null, valor, regra);

    [Fact]
    public void DuplicidadeAgrupaDescricaoNormalizadaNoMesmoDia()
    {
        var a = Conta(4);
        var b = Conta(4) with { Descricao = "  INTERNET  " };
        var result = AnomaliasDetector.Detectar([a, b], Mes);
        result.Should().ContainSingle().Which.Tipo.Should().Be("DuplicidadeProvavel");
        result[0].Evidencias.Select(e => e.ContaPagarId).Should().BeEquivalentTo([a.Id, b.Id]);
    }

    [Fact]
    public void DetectaAssinaturaDuplicadaMesmoComRegrasDeRecorrenciaDistintas()
    {
        AnomaliasDetector.Detectar([Conta(4, regra: Guid.NewGuid()), Conta(4, regra: Guid.NewGuid())], Mes)
            .Should().ContainSingle(a => a.Tipo == "DuplicidadeProvavel");
    }

    [Fact]
    public void NaoComparaRecorrenciasDistintasAoLongoDosMeses()
    {
        AnomaliasDetector.Detectar([Conta(1, regra: Guid.NewGuid()), Conta(2, regra: Guid.NewGuid()),
            Conta(3, regra: Guid.NewGuid()), Conta(4, 500, Guid.NewGuid())], Mes).Should().BeEmpty();
    }

    [Fact]
    public void NaoConfundeResponsaveisContasOuDatasDiferentes()
    {
        var a = Conta(4);
        AnomaliasDetector.Detectar([a, Conta(4) with { ResponsavelId = Guid.NewGuid() },
            Conta(4) with { ContaBancariaId = Guid.NewGuid() }, Conta(4) with { Data = a.Data.AddDays(1) }], Mes)
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData(120, true)]
    [InlineData(119.99, false)]
    public void AumentoRecorrenteUsaMedianaTresMeses(decimal atual, bool alerta)
    {
        var regra = Guid.NewGuid();
        var result = AnomaliasDetector.Detectar([Conta(1, 100, regra), Conta(2, 100, regra),
            Conta(3, 110, regra), Conta(4, atual, regra)], Mes);
        result.Any(a => a.Tipo == "AumentoRecorrente").Should().Be(alerta);
        if (alerta) result.Single(a => a.Tipo == "AumentoRecorrente").ValorBase.Should().Be(100);
        result.Should().Contain(a => a.Tipo == "RevisarRecorrencia");
    }

    [Theory]
    [InlineData(150, true)]
    [InlineData(149.99, false)]
    public void ValorIncomumExigeCinquentaPorCentoECinquentaReais(decimal atual, bool alerta)
    {
        var result = AnomaliasDetector.Detectar([Conta(1), Conta(2), Conta(3), Conta(4, atual)], Mes);
        result.Any(a => a.Tipo == "ValorIncomum").Should().Be(alerta);
    }

    [Fact]
    public void HistoricoInsuficienteOuAmbiguoNaoGeraComparacao()
    {
        AnomaliasDetector.Detectar([Conta(1), Conta(1), Conta(3), Conta(4, 500)], Mes).Should().BeEmpty();
        AnomaliasDetector.Detectar([Conta(1), Conta(2), Conta(2), Conta(3), Conta(4, 500)], Mes).Should().BeEmpty();
    }

    [Fact]
    public void NaoUsaMesFuturoNemHistoricoForaDaJanela()
    {
        AnomaliasDetector.Detectar([Conta(1), Conta(2), Conta(3), Conta(5, 500)], Mes).Should().BeEmpty();
        AnomaliasDetector.Detectar([Conta(1), Conta(2), Conta(3), Conta(4, 500)], Mes.AddMonths(1)).Should().BeEmpty();
    }
}
