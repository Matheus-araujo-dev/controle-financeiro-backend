using ControleFinanceiro.Domain.Financeiro;
using FluentAssertions;

namespace ControleFinanceiro.Domain.Tests.Financeiro;

public sealed class CalendarioBrasilTests
{
    [Theory]
    [InlineData("2026-01-01")] // Ano Novo (fixo)
    [InlineData("2026-12-25")] // Natal (fixo)
    [InlineData("2026-04-21")] // Tiradentes (fixo)
    [InlineData("2026-06-20")] // Sábado
    [InlineData("2026-06-21")] // Domingo
    [InlineData("2026-04-03")] // Sexta-feira Santa (móvel; Páscoa 05/04/2026)
    [InlineData("2026-02-17")] // Terça de Carnaval (móvel)
    [InlineData("2026-02-16")] // Segunda de Carnaval (móvel)
    [InlineData("2026-06-04")] // Corpus Christi (móvel)
    public void EhDiaUtil_FeriadosEFinaisDeSemana_DeveSerFalse(string data)
    {
        CalendarioBrasil.EhDiaUtil(DateOnly.Parse(data)).Should().BeFalse();
    }

    [Theory]
    [InlineData("2026-06-22")] // Segunda comum
    [InlineData("2026-06-23")] // Terça comum
    public void EhDiaUtil_DiaComum_DeveSerTrue(string data)
    {
        CalendarioBrasil.EhDiaUtil(DateOnly.Parse(data)).Should().BeTrue();
    }

    [Fact]
    public void ObterDiaUtil_PrimeiroDiaUtil_DeveSerDiaUtilNoMes()
    {
        var resultado = CalendarioBrasil.ObterDiaUtil(2026, 6, 1);

        resultado.Month.Should().Be(6);
        CalendarioBrasil.EhDiaUtil(resultado).Should().BeTrue();
    }

    [Fact]
    public void ObterDiaUtil_OrdemAlemDoMes_DeveCairNoUltimoDiaUtil()
    {
        var resultado = CalendarioBrasil.ObterDiaUtil(2026, 2, 40);

        resultado.Should().Be(CalendarioBrasil.ObterUltimoDiaUtil(2026, 2));
    }

    [Fact]
    public void ObterUltimoDiaUtil_DeveRetornarDiaUtilDoMes()
    {
        var resultado = CalendarioBrasil.ObterUltimoDiaUtil(2026, 5);

        resultado.Month.Should().Be(5);
        CalendarioBrasil.EhDiaUtil(resultado).Should().BeTrue();
    }
}
