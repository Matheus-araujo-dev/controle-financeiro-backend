using ControleFinanceiro.Domain.Financeiro;
using FluentAssertions;

namespace ControleFinanceiro.Domain.Tests.Financeiro;

public sealed class RegraRecorrenciaTests
{
    [Fact]
    public void CalcularDatasPendentes_DeveGerarMesesFaltantesAteDataInformada()
    {
        var regra = RegraRecorrencia.Criar(
            tipoLancamento: TipoLancamentoRecorrencia.ContaPagar,
            tipoPeriodicidade: TipoPeriodicidadeRecorrencia.Mensal,
            tipoDia: TipoDiaRecorrencia.DiaFixo,
            diaOrdemMensal: 20,
            dataInicio: new DateOnly(2026, 4, 20),
            dataFim: null,
            permiteEdicaoOcorrenciaIndividual: true,
            observacao: "Assinatura mensal",
            templateJson: "{}");

        var datas = regra.CalcularDatasPendentes(
            datasExistentes:
            [
                new DateOnly(2026, 4, 20),
                new DateOnly(2026, 5, 20)
            ],
            ateData: new DateOnly(2026, 7, 31));

        datas.Should().ContainInOrder(
            new DateOnly(2026, 6, 20),
            new DateOnly(2026, 7, 20));
    }

    [Fact]
    public void PausarEEncerrar_DeveriamBloquearGeracaoForaDaJanelaValida()
    {
        var regra = RegraRecorrencia.Criar(
            tipoLancamento: TipoLancamentoRecorrencia.ContaReceber,
            tipoPeriodicidade: TipoPeriodicidadeRecorrencia.Mensal,
            tipoDia: TipoDiaRecorrencia.DiaFixo,
            diaOrdemMensal: 5,
            dataInicio: new DateOnly(2026, 4, 5),
            dataFim: null,
            permiteEdicaoOcorrenciaIndividual: false,
            observacao: null,
            templateJson: "{}");

        regra.Pausar();

        regra.CalcularDatasPendentes(
                datasExistentes: [new DateOnly(2026, 4, 5)],
                ateData: new DateOnly(2026, 6, 30))
            .Should()
            .BeEmpty();

        regra.Retomar();
        regra.Encerrar(new DateOnly(2026, 5, 31));

        regra.DataFim.Should().Be(new DateOnly(2026, 5, 31));
        regra.Ativa.Should().BeFalse();
        regra.CalcularDatasPendentes(
                datasExistentes: [new DateOnly(2026, 4, 5)],
                ateData: new DateOnly(2026, 8, 31))
            .Should()
            .BeEmpty();
    }
}
