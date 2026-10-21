using ControleFinanceiro.Application.Common.Exceptions;
using FluentAssertions;

namespace ControleFinanceiro.Application.Tests.Common.Exceptions;

public sealed class FaturaIndisponivelExceptionTests
{
    [Fact]
    public void FaturaIndisponivelException_FaturaFechada_FormataMensagemCorretamente()
    {
        var ex = new FaturaIndisponivelException("2026-07", faturaEstaPaga: false);

        ex.Message.Should().Contain("07/2026");
        ex.Message.Should().Contain("fechada");
    }

    [Fact]
    public void FaturaIndisponivelException_FaturaLiquidada_FormataMensagemCorretamente()
    {
        var ex = new FaturaIndisponivelException("2026-07", faturaEstaPaga: true);

        ex.Message.Should().Contain("07/2026");
        ex.Message.Should().Contain("liquidada");
    }

    [Fact]
    public void FaturaIndisponivelException_CompetenciaInvalida_UsaCompetenciaSemFormatar()
    {
        var ex = new FaturaIndisponivelException("competencia-invalida-sem-formato", faturaEstaPaga: false);

        ex.Message.Should().NotBeNullOrWhiteSpace();
    }
}
