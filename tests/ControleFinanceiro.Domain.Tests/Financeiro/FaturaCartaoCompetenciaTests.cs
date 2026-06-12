using ControleFinanceiro.Domain.Financeiro;
using FluentAssertions;

namespace ControleFinanceiro.Domain.Tests.Financeiro;

public sealed class FaturaCartaoCompetenciaTests
{
    [Theory]
    [InlineData(2026, 4, 5, 8, 20, "2026-04", 2026, 4, 8, 2026, 4, 20)]
    [InlineData(2026, 4, 9, 8, 20, "2026-05", 2026, 5, 8, 2026, 5, 20)]
    [InlineData(2026, 4, 26, 25, 5, "2026-05", 2026, 5, 25, 2026, 6, 5)]
    public void Calcular_DeveDeterminarCompetenciaFechamentoEVencimento(
        int anoCompra,
        int mesCompra,
        int diaCompra,
        int diaFechamento,
        int diaVencimento,
        string competenciaEsperada,
        int anoFechamento,
        int mesFechamento,
        int diaFechamentoEsperado,
        int anoVencimento,
        int mesVencimento,
        int diaVencimentoEsperado)
    {
        var resultado = FaturaCartaoCompetencia.Calcular(
            new DateOnly(anoCompra, mesCompra, diaCompra),
            diaFechamento,
            diaVencimento);

        resultado.Competencia.Should().Be(competenciaEsperada);
        resultado.DataFechamento.Should().Be(new DateOnly(anoFechamento, mesFechamento, diaFechamentoEsperado));
        resultado.DataVencimento.Should().Be(new DateOnly(anoVencimento, mesVencimento, diaVencimentoEsperado));
    }

    [Theory]
    [InlineData(2026, 4, 15, 5, 15, "2026-04", 2026, 4, 5)]
    [InlineData(2026, 5, 5, 25, 5, "2026-04", 2026, 4, 25)]
    public void CalcularPorDataVencimento_DeveDeterminarCompetenciaDaFatura(
        int anoVencimento,
        int mesVencimento,
        int diaVencimentoReal,
        int diaFechamento,
        int diaVencimentoConfigurado,
        string competenciaEsperada,
        int anoFechamento,
        int mesFechamento,
        int diaFechamentoEsperado)
    {
        var resultado = FaturaCartaoCompetencia.CalcularPorDataVencimento(
            new DateOnly(anoVencimento, mesVencimento, diaVencimentoReal),
            diaFechamento,
            diaVencimentoConfigurado);

        resultado.Competencia.Should().Be(competenciaEsperada);
        resultado.DataFechamento.Should().Be(new DateOnly(anoFechamento, mesFechamento, diaFechamentoEsperado));
        resultado.DataVencimento.Should().Be(new DateOnly(anoVencimento, mesVencimento, diaVencimentoReal));
    }
}
