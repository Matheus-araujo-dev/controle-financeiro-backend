using ControleFinanceiro.Domain.Financeiro;
using FluentAssertions;

namespace ControleFinanceiro.Domain.Tests.Financeiro;

public sealed class FechamentoMensalTests
{
    [Fact]
    public void Fechar_DeveCriarSnapshotComResponsavelEStatusFechado()
    {
        var responsavelId = Guid.NewGuid();
        var fechadoEm = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

        var fechamento = FechamentoMensal.Fechar(
            "2026-09", responsavelId, fechadoEm,
            totalReceitas: 5000m, totalDespesas: 3200m,
            totalPendente: 0m, totalVencido: 0m,
            quantidadeLancamentos: 42, quantidadeBloqueios: 0);

        fechamento.Status.Should().Be(StatusFechamentoMensal.Fechado);
        fechamento.Competencia.Should().Be("2026-09");
        fechamento.FechadoPorUsuarioId.Should().Be(responsavelId);
        fechamento.FechadoEmUtc.Should().Be(fechadoEm);
        fechamento.SaldoSnapshot.Should().Be(1800m);
    }

    [Fact]
    public void Fechar_ComBloqueios_DeveFalhar()
    {
        var action = () => FechamentoMensal.Fechar(
            "2026-09", Guid.NewGuid(), DateTimeOffset.UtcNow,
            100m, 80m, 20m, 0m, 2, quantidadeBloqueios: 1);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*bloqueio*");
    }

    [Fact]
    public void Reabrir_DeveExigirJustificativaERegistrarResponsavel()
    {
        var fechamento = FechamentoMensal.Fechar(
            "2026-09", Guid.NewGuid(), DateTimeOffset.UtcNow,
            100m, 80m, 0m, 0m, 2, 0);

        var semJustificativa = () => fechamento.Reabrir(Guid.NewGuid(), DateTimeOffset.UtcNow, " ");
        semJustificativa.Should().Throw<ArgumentException>();

        var responsavelReabertura = Guid.NewGuid();
        var reabertoEm = new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.Zero);
        fechamento.Reabrir(responsavelReabertura, reabertoEm, "Ajuste de lançamento atrasado");

        fechamento.Status.Should().Be(StatusFechamentoMensal.Reaberto);
        fechamento.ReabertoPorUsuarioId.Should().Be(responsavelReabertura);
        fechamento.ReabertoEmUtc.Should().Be(reabertoEm);
        fechamento.JustificativaReabertura.Should().Be("Ajuste de lançamento atrasado");
    }

    [Fact]
    public void FecharNovamente_DeveAtualizarSnapshotELimparReabertura()
    {
        var fechamento = FechamentoMensal.Fechar(
            "2026-09", Guid.NewGuid(), DateTimeOffset.UtcNow,
            100m, 80m, 0m, 0m, 2, 0);
        fechamento.Reabrir(Guid.NewGuid(), DateTimeOffset.UtcNow, "Correção necessária");
        var novoResponsavel = Guid.NewGuid();
        var novaData = DateTimeOffset.UtcNow.AddHours(1);

        fechamento.FecharNovamente(novoResponsavel, novaData, 120m, 90m, 0m, 0m, 3, 0);

        fechamento.Status.Should().Be(StatusFechamentoMensal.Fechado);
        fechamento.FechadoPorUsuarioId.Should().Be(novoResponsavel);
        fechamento.FechadoEmUtc.Should().Be(novaData);
        fechamento.TotalReceitasSnapshot.Should().Be(120m);
        fechamento.TotalDespesasSnapshot.Should().Be(90m);
        fechamento.SaldoSnapshot.Should().Be(30m);
        fechamento.QuantidadeLancamentosSnapshot.Should().Be(3);
        fechamento.ReabertoPorUsuarioId.Should().BeNull();
        fechamento.ReabertoEmUtc.Should().BeNull();
        fechamento.JustificativaReabertura.Should().BeNull();
    }
}
