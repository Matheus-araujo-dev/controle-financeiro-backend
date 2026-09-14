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

    [Fact]
    public void Retomar_NaoGeraMesesAnterioresAoMesDaRetomada()
    {
        var inicio = new DateOnly(DateTime.Today.Year - 2, 1, 5);
        var regra = RegraRecorrencia.Criar(TipoLancamentoRecorrencia.ContaPagar,
            TipoPeriodicidadeRecorrencia.Mensal, TipoDiaRecorrencia.DiaFixo, 5,
            inicio, null, true, null, "{}");
        regra.Pausar();
        regra.Retomar();
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var inicioMes = new DateOnly(hoje.Year, hoje.Month, 1);
        var datas = regra.CalcularDatasPendentes([], inicioMes.AddMonths(7).AddDays(-1));
        datas.Should().HaveCount(7);
        datas.Should().OnlyContain(d => d >= inicioMes);
    }

    // ── Testes de criação e validação ──────────────────────────────────────

    [Fact]
    public void Criar_DeveInicializarComoAtivaNaoEncerrada()
    {
        var regra = CriarRegra();

        regra.Ativa.Should().BeTrue();
        regra.Encerrada.Should().BeFalse();
        regra.GerarAPartirDe.Should().BeNull();
    }

    [Fact]
    public void Criar_ComDataFim_DeveTerDataFimDefinida()
    {
        var regra = RegraRecorrencia.Criar(
            TipoLancamentoRecorrencia.ContaPagar,
            TipoPeriodicidadeRecorrencia.Mensal,
            TipoDiaRecorrencia.DiaFixo,
            10,
            new DateOnly(2026, 1, 10),
            new DateOnly(2026, 12, 31),
            false,
            "Com data fim",
            "{}");

        regra.DataFim.Should().Be(new DateOnly(2026, 12, 31));
    }

    [Fact]
    public void Criar_DataFimAnteriorADataInicio_DeveLancarExcecao()
    {
        var act = () => RegraRecorrencia.Criar(
            TipoLancamentoRecorrencia.ContaPagar,
            TipoPeriodicidadeRecorrencia.Mensal,
            TipoDiaRecorrencia.DiaFixo,
            10,
            new DateOnly(2026, 6, 10),
            new DateOnly(2026, 1, 1),
            false,
            null,
            "{}");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Data fim*maior ou igual*");
    }

    [Fact]
    public void Criar_DiaOrdemMensalInvalido_DeveLancarExcecao()
    {
        var actZero = () => RegraRecorrencia.Criar(
            TipoLancamentoRecorrencia.ContaPagar,
            TipoPeriodicidadeRecorrencia.Mensal,
            TipoDiaRecorrencia.DiaFixo,
            0, new DateOnly(2026, 1, 1), null, false, null, "{}");

        actZero.Should().Throw<ArgumentException>()
            .WithMessage("*Dia de ordem mensal*");

        var act32 = () => RegraRecorrencia.Criar(
            TipoLancamentoRecorrencia.ContaPagar,
            TipoPeriodicidadeRecorrencia.Mensal,
            TipoDiaRecorrencia.DiaFixo,
            32, new DateOnly(2026, 1, 1), null, false, null, "{}");

        act32.Should().Throw<ArgumentException>()
            .WithMessage("*Dia de ordem mensal*");
    }

    [Fact]
    public void Criar_TemplateJsonVazio_DeveLancarExcecao()
    {
        var act = () => RegraRecorrencia.Criar(
            TipoLancamentoRecorrencia.ContaPagar,
            TipoPeriodicidadeRecorrencia.Mensal,
            TipoDiaRecorrencia.DiaFixo,
            10, new DateOnly(2026, 1, 1), null, false, null, "");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Template*obrigatório*");
    }

    [Fact]
    public void Criar_ObservacaoComEspacos_DeveSerTrimada()
    {
        var regra = RegraRecorrencia.Criar(
            TipoLancamentoRecorrencia.ContaPagar,
            TipoPeriodicidadeRecorrencia.Mensal,
            TipoDiaRecorrencia.DiaFixo,
            10, new DateOnly(2026, 1, 1), null, false,
            "  obs com espaços  ", "{}");

        regra.Observacao.Should().Be("obs com espaços");
    }

    [Fact]
    public void Criar_ObservacaoApenasEspacos_DeveSerNull()
    {
        var regra = RegraRecorrencia.Criar(
            TipoLancamentoRecorrencia.ContaPagar,
            TipoPeriodicidadeRecorrencia.Mensal,
            TipoDiaRecorrencia.DiaFixo,
            10, new DateOnly(2026, 1, 1), null, false,
            "   ", "{}");

        regra.Observacao.Should().BeNull();
    }

    // ── Testes de Pausar ────────────────────────────────────────────────

    [Fact]
    public void Pausar_DeveDesativarRegra()
    {
        var regra = CriarRegra();

        regra.Pausar();

        regra.Ativa.Should().BeFalse();
        regra.Encerrada.Should().BeFalse();
    }

    [Fact]
    public void Pausar_RecorrenciaEncerrada_DeveLancarExcecao()
    {
        var regra = CriarRegra();
        regra.Encerrar(new DateOnly(2026, 6, 30));

        var act = () => regra.Pausar();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*encerrada*");
    }

    // ── Testes de Retomar ────────────────────────────────────────────────

    [Fact]
    public void Retomar_DeveAtivarEDefinirGerarAPartirDe()
    {
        var regra = CriarRegra();
        regra.Pausar();

        regra.Retomar();

        regra.Ativa.Should().BeTrue();
        regra.GerarAPartirDe.Should().NotBeNull();
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        regra.GerarAPartirDe.Should().Be(new DateOnly(hoje.Year, hoje.Month, 1));
    }

    [Fact]
    public void Retomar_RecorrenciaJaAtiva_NaoDeveFazerNada()
    {
        var regra = CriarRegra();

        regra.Retomar();

        regra.Ativa.Should().BeTrue();
        regra.GerarAPartirDe.Should().BeNull();
    }

    [Fact]
    public void Retomar_RecorrenciaEncerrada_DeveLancarExcecao()
    {
        var regra = CriarRegra();
        regra.Encerrar(new DateOnly(2026, 6, 30));

        var act = () => regra.Retomar();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*encerrada*");
    }

    // ── Testes de Encerrar ──────────────────────────────────────────────

    [Fact]
    public void Encerrar_DeveDefinirDataFimEDesativar()
    {
        var regra = CriarRegra();

        regra.Encerrar(new DateOnly(2026, 8, 31));

        regra.Ativa.Should().BeFalse();
        regra.Encerrada.Should().BeTrue();
        regra.DataFim.Should().Be(new DateOnly(2026, 8, 31));
    }

    [Fact]
    public void Encerrar_DataFimAnteriorADataInicio_DeveLancarExcecao()
    {
        var regra = RegraRecorrencia.Criar(
            TipoLancamentoRecorrencia.ContaPagar,
            TipoPeriodicidadeRecorrencia.Mensal,
            TipoDiaRecorrencia.DiaFixo,
            10,
            new DateOnly(2026, 6, 10),
            null, false, null, "{}");

        var act = () => regra.Encerrar(new DateOnly(2026, 1, 1));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Data fim*maior ou igual*");
    }

    // ── Testes de Atualizar ────────────────────────────────────────────

    [Fact]
    public void Atualizar_DeveAlterarCamposSemMudarTipoLancamento()
    {
        var regra = RegraRecorrencia.Criar(
            TipoLancamentoRecorrencia.ContaPagar,
            TipoPeriodicidadeRecorrencia.Mensal,
            TipoDiaRecorrencia.DiaFixo,
            10,
            new DateOnly(2026, 1, 10),
            null, false, null, "{}");

        regra.Atualizar(
            TipoPeriodicidadeRecorrencia.Mensal,
            TipoDiaRecorrencia.DiaUtil,
            15,
            new DateOnly(2026, 2, 1),
            new DateOnly(2026, 12, 31),
            true,
            "Atualizada",
            "{\"novo\": true}");

        regra.TipoLancamento.Should().Be(TipoLancamentoRecorrencia.ContaPagar);
        regra.TipoDia.Should().Be(TipoDiaRecorrencia.DiaUtil);
        regra.DiaOrdemMensal.Should().Be(15);
        regra.DataInicio.Should().Be(new DateOnly(2026, 2, 1));
        regra.DataFim.Should().Be(new DateOnly(2026, 12, 31));
        regra.PermiteEdicaoOcorrenciaIndividual.Should().BeTrue();
        regra.Observacao.Should().Be("Atualizada");
        regra.TemplateJson.Should().Be("{\"novo\": true}");
    }

    // ── Testes de CalcularDatasPendentes (edge cases) ────────────────────

    [Fact]
    public void CalcularDatasPendentes_RegraInativa_DeveRetornarVazio()
    {
        var regra = CriarRegra();
        regra.Pausar();

        var datas = regra.CalcularDatasPendentes([], new DateOnly(2026, 12, 31));

        datas.Should().BeEmpty();
    }

    [Fact]
    public void CalcularDatasPendentes_AteDataAnteriorAoInicio_DeveRetornarVazio()
    {
        var regra = RegraRecorrencia.Criar(
            TipoLancamentoRecorrencia.ContaPagar,
            TipoPeriodicidadeRecorrencia.Mensal,
            TipoDiaRecorrencia.DiaFixo,
            10,
            new DateOnly(2026, 6, 10),
            null, false, null, "{}");

        var datas = regra.CalcularDatasPendentes([], new DateOnly(2026, 3, 31));

        datas.Should().BeEmpty();
    }

    [Fact]
    public void CalcularDatasPendentes_DataFimLimitaGeracao()
    {
        var regra = RegraRecorrencia.Criar(
            TipoLancamentoRecorrencia.ContaPagar,
            TipoPeriodicidadeRecorrencia.Mensal,
            TipoDiaRecorrencia.DiaFixo,
            15,
            new DateOnly(2026, 1, 15),
            new DateOnly(2026, 3, 31),
            false, null, "{}");

        var datas = regra.CalcularDatasPendentes([], new DateOnly(2026, 12, 31));

        datas.Should().HaveCount(3);
        datas.Should().ContainInOrder(
            new DateOnly(2026, 1, 15),
            new DateOnly(2026, 2, 15),
            new DateOnly(2026, 3, 15));
    }

    [Fact]
    public void CalcularDatasPendentes_NaoGeraParaDatasJaExistentes()
    {
        var regra = RegraRecorrencia.Criar(
            TipoLancamentoRecorrencia.ContaPagar,
            TipoPeriodicidadeRecorrencia.Mensal,
            TipoDiaRecorrencia.DiaFixo,
            10,
            new DateOnly(2026, 1, 10),
            null, false, null, "{}");

        var datasExistentes = new List<DateOnly>
        {
            new(2026, 1, 10),
            new(2026, 2, 10),
            new(2026, 3, 10),
        };

        var datas = regra.CalcularDatasPendentes(datasExistentes, new DateOnly(2026, 5, 31));

        datas.Should().HaveCount(2);
        datas.Should().ContainInOrder(
            new DateOnly(2026, 4, 10),
            new DateOnly(2026, 5, 10));
    }

    [Fact]
    public void CalcularDatasPendentes_DiaFixo31_AjustaParaUltimoDiaDoMes()
    {
        var regra = RegraRecorrencia.Criar(
            TipoLancamentoRecorrencia.ContaPagar,
            TipoPeriodicidadeRecorrencia.Mensal,
            TipoDiaRecorrencia.DiaFixo,
            31,
            new DateOnly(2026, 1, 31),
            null, false, null, "{}");

        var datas = regra.CalcularDatasPendentes([], new DateOnly(2026, 4, 30));

        // Jan=31, Fev=28, Mar=31, Abr=30
        datas.Should().ContainInOrder(
            new DateOnly(2026, 1, 31),
            new DateOnly(2026, 2, 28),
            new DateOnly(2026, 3, 31),
            new DateOnly(2026, 4, 30));
    }

    [Fact]
    public void CalcularDatasPendentes_DiaUtil_DeveCalcularUsandoCalendarioBrasil()
    {
        var regra = RegraRecorrencia.Criar(
            TipoLancamentoRecorrencia.ContaReceber,
            TipoPeriodicidadeRecorrencia.Mensal,
            TipoDiaRecorrencia.DiaUtil,
            5,
            new DateOnly(2026, 1, 1),
            null, false, null, "{}");

        var datas = regra.CalcularDatasPendentes([], new DateOnly(2026, 2, 28));

        // O resultado depende do CalendarioBrasil; devemos ter exatamente 2 datas
        datas.Should().HaveCount(2);
        // Cada data deve ser um dia útil
        foreach (var d in datas)
        {
            d.DayOfWeek.Should().NotBe(DayOfWeek.Saturday);
            d.DayOfWeek.Should().NotBe(DayOfWeek.Sunday);
        }
    }

    [Fact]
    public void CalcularDataParaMes_DiaFixo_DeveRetornarDiaCorreto()
    {
        var regra = CriarRegra(diaOrdemMensal: 25);

        var data = regra.CalcularDataParaMes(2026, 7);

        data.Should().Be(new DateOnly(2026, 7, 25));
    }

    [Fact]
    public void CalcularDataParaMes_DiaFixo_FevDia30_AjustaParaDia28()
    {
        var regra = CriarRegra(diaOrdemMensal: 30);

        var data = regra.CalcularDataParaMes(2026, 2);

        data.Should().Be(new DateOnly(2026, 2, 28));
    }

    [Fact]
    public void CalcularDataParaMes_DiaFixo_AnoAnoBissexto_FevDia30_AjustaParaDia29()
    {
        var regra = CriarRegra(diaOrdemMensal: 30);

        var data = regra.CalcularDataParaMes(2028, 2); // 2028 é bissexto

        data.Should().Be(new DateOnly(2028, 2, 29));
    }

    // ── Testes do ciclo completo Pausar → Retomar → Encerrar ─────────────

    [Fact]
    public void CicloCompleto_PausarRetomarEncerrar_DeveManterConsistencia()
    {
        var regra = CriarRegra();
        regra.Ativa.Should().BeTrue();

        // Pausar
        regra.Pausar();
        regra.Ativa.Should().BeFalse();
        regra.Encerrada.Should().BeFalse();

        // Retomar
        regra.Retomar();
        regra.Ativa.Should().BeTrue();
        regra.GerarAPartirDe.Should().NotBeNull();

        // Encerrar
        regra.Encerrar(new DateOnly(2026, 12, 31));
        regra.Ativa.Should().BeFalse();
        regra.Encerrada.Should().BeTrue();

        // Não pode pausar depois de encerrar
        var actPausar = () => regra.Pausar();
        actPausar.Should().Throw<InvalidOperationException>();

        // Não pode retomar depois de encerrar
        var actRetomar = () => regra.Retomar();
        actRetomar.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CalcularDatasPendentes_AposRetomada_RespestaGerarAPartirDe()
    {
        // Regra com início antigo
        var regra = RegraRecorrencia.Criar(
            TipoLancamentoRecorrencia.ContaPagar,
            TipoPeriodicidadeRecorrencia.Mensal,
            TipoDiaRecorrencia.DiaFixo,
            10,
            new DateOnly(2024, 1, 10),
            null, false, null, "{}");

        regra.Pausar();
        regra.Retomar();

        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var inicioMesAtual = new DateOnly(hoje.Year, hoje.Month, 1);

        // Gerar até 3 meses à frente
        var datas = regra.CalcularDatasPendentes([], inicioMesAtual.AddMonths(3).AddDays(-1));

        // Todas devem ser >= primeiro dia do mês da retomada
        datas.Should().OnlyContain(d => d >= inicioMesAtual);
    }

    // ── Helper ──────────────────────────────────────────────────────────

    private static RegraRecorrencia CriarRegra(
        TipoLancamentoRecorrencia tipo = TipoLancamentoRecorrencia.ContaPagar,
        int diaOrdemMensal = 10,
        DateOnly? dataInicio = null,
        DateOnly? dataFim = null)
    {
        return RegraRecorrencia.Criar(
            tipo,
            TipoPeriodicidadeRecorrencia.Mensal,
            TipoDiaRecorrencia.DiaFixo,
            diaOrdemMensal,
            dataInicio ?? new DateOnly(2026, 1, 10),
            dataFim,
            false,
            null,
            "{}");
    }
}
