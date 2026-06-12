using ControleFinanceiro.Domain.Financeiro;
using FluentAssertions;

namespace ControleFinanceiro.Domain.Tests.Financeiro;

public sealed class FaturaCartaoCompetenciaRegraFechamentoTests
{
    // Regra: cartão fecha dia 10 e vence dia 20 → compras antes do dia 10 entram na
    // fatura do mês (paga dia 20); compras no dia 10 em diante vão para o mês seguinte.
    [Theory]
    [InlineData("2026-04-09", "2026-04", "2026-04-20")]
    [InlineData("2026-04-01", "2026-04", "2026-04-20")]
    [InlineData("2026-04-10", "2026-05", "2026-05-20")]
    [InlineData("2026-04-11", "2026-05", "2026-05-20")]
    public void Calcular_DeveRespeitarDiaDeFechamento(string dataCompra, string competenciaEsperada, string vencimentoEsperado)
    {
        var resultado = FaturaCartaoCompetencia.Calcular(DateOnly.Parse(dataCompra), diaFechamento: 10, diaVencimento: 20);

        resultado.Competencia.Should().Be(competenciaEsperada);
        resultado.DataVencimento.Should().Be(DateOnly.Parse(vencimentoEsperado));
    }

    [Fact]
    public void Calcular_QuandoVencimentoAntesDoFechamento_DeveVencerNoMesSeguinte()
    {
        // Fecha dia 25, vence dia 5: compra dia 10 entra na competência do mês,
        // mas o pagamento só ocorre no dia 5 do mês seguinte.
        var resultado = FaturaCartaoCompetencia.Calcular(new DateOnly(2026, 4, 10), diaFechamento: 25, diaVencimento: 5);

        resultado.Competencia.Should().Be("2026-04");
        resultado.DataFechamento.Should().Be(new DateOnly(2026, 4, 25));
        resultado.DataVencimento.Should().Be(new DateOnly(2026, 5, 5));
    }
}

public sealed class FaturaCartaoFechadaTests
{
    private static FaturaCartao CriarFatura(DateOnly fechamento) =>
        FaturaCartao.Criar(Guid.NewGuid(), "2026-04", fechamento, fechamento.AddDays(10), 100m, null);

    [Fact]
    public void EstaFechada_DeveConsiderarDataDeFechamento()
    {
        var fatura = CriarFatura(new DateOnly(2026, 4, 10));

        fatura.EstaFechada(new DateOnly(2026, 4, 10)).Should().BeFalse();
        fatura.EstaFechada(new DateOnly(2026, 4, 11)).Should().BeTrue();
    }

    [Fact]
    public void AtualizarValorTotal_NaoAlteraDatas()
    {
        var fatura = CriarFatura(new DateOnly(2026, 4, 10));

        fatura.AtualizarValorTotal(250m);

        fatura.ValorTotal.Should().Be(250m);
        fatura.DataFechamento.Should().Be(new DateOnly(2026, 4, 10));
        fatura.DataVencimento.Should().Be(new DateOnly(2026, 4, 20));
    }
}

public sealed class ContaPagarEmFaturaTests
{
    private static ContaPagar CriarConta(Guid? cartaoId, Guid statusContaId) =>
        ContaPagar.Criar(
            numeroDocumento: null,
            dataEmissao: new DateOnly(2026, 4, 5),
            responsavelCompraId: Guid.NewGuid(),
            recebedorId: Guid.NewGuid(),
            dataVencimento: new DateOnly(2026, 4, 20),
            formaPagamentoId: Guid.NewGuid(),
            cartaoId: cartaoId,
            contaBancariaId: null,
            valorOriginal: 100m,
            valorDesconto: 0m,
            valorJuros: 0m,
            valorMulta: 0m,
            quantidadeParcelas: 1,
            numeroParcela: 1,
            grupoParcelamentoId: null,
            origemCompraPlanejadaId: null,
            descricao: "Compra de teste",
            observacao: null,
            statusContaId: statusContaId,
            ehRecorrente: false,
            regraRecorrenciaId: null,
            origem: OrigemLancamento.Manual,
            rateios: [RateioPlano.Create(Guid.NewGuid(), 100m)]);

    [Fact]
    public void Criar_ComCartao_DeveNascerEmFatura()
    {
        var conta = CriarConta(Guid.NewGuid(), StatusConta.PendenteId);

        conta.StatusContaId.Should().Be(StatusConta.EmFaturaId);
    }

    [Fact]
    public void Criar_SemCartao_DevePermanecerPendente()
    {
        var conta = CriarConta(null, StatusConta.PendenteId);

        conta.StatusContaId.Should().Be(StatusConta.PendenteId);
    }

    [Fact]
    public void Criar_ComCartaoEStatusLiquidada_NaoDeveCoagir()
    {
        var conta = CriarConta(Guid.NewGuid(), StatusConta.LiquidadaId);

        conta.StatusContaId.Should().Be(StatusConta.LiquidadaId);
    }
}
